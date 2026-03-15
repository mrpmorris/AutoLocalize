using Fody;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Morris.AutoLocalize;
using Morris.AutoLocalize.Fody;
using Morris.AutoLocalizeTests.Extensions;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Resources;
using System.Text;

namespace Morris.AutoLocalizeTests.ModuleWeaverTests;

internal static class WeaverExecutor
{
	private static readonly MetadataReference AutoLocalizeMetadataReference =
		MetadataReference
			.CreateFromFile(typeof(AutoLocalizeValidationAttributesAttribute).Assembly.Location);

	private static readonly MetadataReference SystemComponentModelDataAnnotationsMetadataReference =
		MetadataReference
			.CreateFromFile(typeof(RequiredAttribute).Assembly.Location);


	static WeaverExecutor()
	{
		string[] filePaths = Directory.GetFiles(Path.GetTempPath(), "*.Morris.AutoLocalize.Tests.dll");
		Parallel.ForEach(filePaths, x => File.Delete(x));
	}

	public static void Execute(
		string sourceCode,
		out Fody.TestResult testResult,
		out string? manifest,
		IEnumerable<KeyValuePair<string, string?>>? assemblyResourceValuesToCreate = null)
	{
		assemblyResourceValuesToCreate ??= [new("AutoLocalize_Required", "The {0} field is required.")];

		Guid uniqueId = Guid.NewGuid();
		SyntaxTree unitTestSyntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
		SyntaxTree resourceClassSyntaxTree = CSharpSyntaxTree.ParseText(ResourceClassSourceCode);

		CSharpCompilation compilation = Compile(
			assemblyName: "UnitTest",
			uniqueId: uniqueId,
			syntaxTrees: [unitTestSyntaxTree, resourceClassSyntaxTree]
		);

		AssertNoCompileDiagnostics(compilation);

		string projectFilePath = Path.Combine(
			Path.GetTempPath(),
			$"{uniqueId}.csproj"
		);

		string assemblyFilePath = Path.ChangeExtension(projectFilePath, "Morris.AutoLocalize.Tests.dll");
		string manifestFilePath = Path.ChangeExtension(projectFilePath, "Morris.AutoRegister.csv");

		try
		{
			IEnumerable<ResourceDescription> manifestResources =
			[
				CreateResources("UnitTest.AppStrings.resources", assemblyResourceValuesToCreate)
			];

			EmitResult emitResult;
			using (FileStream peStream = File.Create(assemblyFilePath))
			{
				emitResult = compilation.Emit(
					peStream: peStream,
					pdbStream: null,
					xmlDocumentationStream: null,
					win32Resources: null,
					manifestResources: manifestResources,
					options: new EmitOptions(),
					cancellationToken: CancellationToken.None
				);
			}

			if (!emitResult.Success)
			{
				string diagnosticsText = string.Join(
					Environment.NewLine,
					emitResult.Diagnostics.Select(x => x.ToString())
				);

				Assert.Fail("Emit failed:" + Environment.NewLine + diagnosticsText);
			}

			var weaver = new ModuleWeaver {
				ProjectFilePath = projectFilePath
			};

			testResult = weaver.ExecuteTestRun(assemblyFilePath);

			manifest =
				!File.Exists(manifestFilePath)
				? null
				: File.ReadAllText(manifestFilePath);
		}
		finally
		{
			if (File.Exists(assemblyFilePath))
				File.Delete(assemblyFilePath);
		}
	}

	private static ResourceDescription CreateResources(
		string manifestResourceName,
		IEnumerable<KeyValuePair<string, string?>>? resourceValues) =>
		new ResourceDescription(
			resourceName: manifestResourceName,
			dataProvider: () =>
			{
				var memoryStream = new MemoryStream();

				var writer = new ResourceWriter(memoryStream);
				foreach (KeyValuePair<string, string?> kvp in resourceValues ?? [])
					writer.AddResource(kvp.Key, kvp.Value);
				writer.Generate();

				memoryStream.Position = 0;
				return memoryStream;
			},
			isPublic: true
		);

	private static CSharpCompilation Compile(
		string assemblyName,
		Guid uniqueId,
		ImmutableArray<SyntaxTree> syntaxTrees)
	{
		CSharpCompilation result = CSharpCompilation
			.Create(
				assemblyName: $"{assemblyName}{uniqueId}",
				syntaxTrees: syntaxTrees,
				references: GetMetadataReferences(),
				options: new CSharpCompilationOptions(
					OutputKind.DynamicallyLinkedLibrary,
					reportSuppressedDiagnostics: true
				)
			);

		return result;
	}

	private static IEnumerable<MetadataReference> GetMetadataReferences() =>
		Basic.Reference.Assemblies.Net90.References
		.All
		.Union([AutoLocalizeMetadataReference, SystemComponentModelDataAnnotationsMetadataReference]);

	private static void AssertNoCompileDiagnostics(CSharpCompilation compilation)
	{
		ImmutableArray<Diagnostic> diagnostics =
			compilation
				.GetDiagnostics()
				.Where(x => x.DefaultSeverity != DiagnosticSeverity.Hidden)
				.ToImmutableArray();

		if (!diagnostics.Any())
			return;

		var builder = new StringBuilder();

		foreach (Diagnostic diagnostic in diagnostics)
			builder.AppendLine(diagnostic.ToString());

		Assert.Fail("The following compiler errors were found:\r\n" + builder);
	}

	private const string ResourceClassSourceCode = $$"""
namespace UnitTest;

public class AppStrings;
""";
}