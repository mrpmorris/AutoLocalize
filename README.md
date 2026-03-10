![](./Images/logo-small.png)
# AutoLocalize

AutoLocalize is a [Fody](https://github.com/Fody/Fody) weaver that fills in
`ErrorMessageResourceType` and `ErrorMessageResourceName` for your
`ValidationAttribute`-based annotations so you only declare your resource type once
per assembly.

## Getting started

1. Install Fody (if you do not already use it) and AutoLocalize:
   ```xml
   <ItemGroup>
     <PackageReference Include="Fody" Version="6.*" PrivateAssets="All" />
     <PackageReference Include="Morris.AutoLocalize.Fody" Version="1.*" />
   </ItemGroup>
   ```
2. Build once. Fody will create a `FodyWeavers.xml` file in your project if it does
   not exist.
3. Add AutoLocalize to `FodyWeavers.xml`, then build again so weaving runs.
   ```xml
   <Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
     <Morris.AutoLocalize />
   </Weavers>
   ```
4. Specify the assembly-level attribute that tells the weaver which resource class
   to use. The default `ErrorMessageResourceNamePrefix` is `"Validation_"` if you do
   not set it.
   ```csharp
   using Morris.AutoLocalize;
   using System.ComponentModel.DataAnnotations;

   [assembly: AutoLocalizeValidationAttributes(
       typeof(Resources.ValidationMessages),
       ErrorMessageResourceNamePrefix = "Validation_")]

   public class Person
   {
       [Required]
       [StringLength(50, MinimumLength = 2)]
       public string Name { get; set; } = string.Empty;
   }
   ```

## What the weaver does

- Finds every field/property decorated with a `ValidationAttribute`.
- If `ErrorMessageResourceType` is not set, it is assigned the type you specified in
  `AutoLocalizeValidationAttributes`.
- If `ErrorMessageResourceName` is not set, it is set to
  `{ErrorMessageResourceNamePrefix}{AttributeNameWithoutSuffix}` (for example,
  `RequiredAttribute` -> `Validation_Required` by default, because the default prefix
  is `Validation_`).
- If only `ErrorMessageResourceName` is set (e.g., to use a specific key in your
  default resource file), AutoLocalize sets only the resource type.
- If both `ErrorMessageResourceType` and `ErrorMessageResourceName` are set (e.g., a
  custom message from another resource file), AutoLocalize leaves the attribute
  untouched.
- If `ErrorMessageResourceType` is already set (with or without a name), AutoLocalize
  does not alter the attribute.
- The assembly reference to `Morris.AutoLocalize` and the marker attribute are
  removed after weaving, so your runtime assembly has no extra dependencies.

## Manifest of resource keys

During weaving a CSV file named after your project (e.g.
`MyProject.Morris.AutoLocalize.csv`) is written next to the project file. It
contains all resource keys the weaver added or discovered:

```
ErrorMessageResourceName
Validation_Required
Validation_StringLength
```

Use this manifest to ensure your resource file contains entries for each key.
