![](./Images/logo-small.png)
# AutoLocalize

AutoLocalize is a [Fody](https://github.com/Fody/Fody) weaver that fills in
`ErrorMessageResourceType` and `ErrorMessageResourceName` for your
`ValidationAttribute`-based annotations so you only declare your resource type once
per assembly.

## Getting started

1. Add the packages to your project (Fody must already be installed for weaving).
   ```xml
   <ItemGroup>
     <PackageReference Include="Fody" Version="6.*" PrivateAssets="All" />
     <PackageReference Include="Morris.AutoLocalize.Fody" Version="1.*" />
   </ItemGroup>
   ```
2. Enable the weaver in `FodyWeavers.xml`.
   ```xml
   <Weavers xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:noNamespaceSchemaLocation="FodyWeavers.xsd">
     <Morris.AutoLocalize />
   </Weavers>
   ```
3. Specify the assembly-level attribute that tells the weaver which resource class
   to use, and optionally the prefix for resource keys.
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
  `RequiredAttribute` -> `Validation_Required` by default).
- Existing `ErrorMessageResourceName` values are preserved; only the resource type is
  updated.
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
