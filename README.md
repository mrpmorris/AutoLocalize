# AutoLocalize
![](./Images/logo-small.png)

[![NuGet version](https://img.shields.io/nuget/v/Morris.AutoLocalize.Fody.svg)](https://www.nuget.org/packages/Morris.AutoLocalize.Fody)
[![NuGet downloads](https://img.shields.io/nuget/dt/Morris.AutoLocalize.Fody.svg)](https://www.nuget.org/packages/Morris.AutoLocalize.Fody)

AutoLocalize is a [Fody](https://github.com/Fody/Fody) weaver that fills in
`ErrorMessageResourceType` and `ErrorMessageResourceName` for your
`ValidationAttribute` based annotations so you only declare your resource type once
per assembly.

## Problem description
It's not possible to have `DataAnnotations` validation attributes automatically pick
up the current locale and present translated messages.

If you have translated resource files then you can set `ErrorMessageResourceType`
and `ErrorMessageResourceName` on your validation attributes, but when you have
thousands of them this can become troublesome and error-prone.

## Solution
AutoLocalize allows you to specify `ErrorMessageResourceType` at project level,
which will then be used on every instance of validation attributes that do not
already have that property set. It will also add `ErrorMessageResourceName` based
on a convention.

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
   to use. The default `ErrorMessageResourceNamePrefix` is `"AutoLocalize_"` if you do
   not set it.
   ```csharp
   using Morris.AutoLocalize;
   using System.ComponentModel.DataAnnotations;

   [assembly: AutoLocalizeValidationAttributes(
       typeof(Resources.ValidationMessages),
       ErrorMessageResourceNamePrefix = "AutoLocalize_")]

   public class Person
   {
       [Required]
       [StringLength(50, MinimumLength = 2)]
       public string Name { get; set; } = string.Empty;
   }
   ```

**Note:** In case you don't know, to get a class for your resx file you need to set its
`Custom Tool` property to `PublicResXFileCodeGenerator`.

## What the weaver does

- Finds every field/property decorated with an attribute descended from `ValidationAttribute`.
- Ensures the `ErrorMessageResourceType` and `ErrorMessageResourceName` properties are set on each attribute.
- Removes the `[assembly:AutoLocalizeValidationAttributes]` attribute from your build output.
- Removes the assembly reference to `Morris.AutoLocalize` from your build output, so your runtime assembly has no extra dependencies.

## ValidationAttribute update rules

- `ErrorMessageResourceType` is set to the value specified in the
       `[assembly:AutoLocalizeValidationAttributes({value here})]` attribute you added to your project.
- `ErrorMessageResourceName` is set to `{Prefix}_{ShortAttributeName}`
- If either the `ErrorMessageResourceType` or `ErrorMessage` properties 
       are set on the attribute then it is assumed you want a specific error message,
       so AutoLocalize will leave the validation attribute untouched.


### Resource automatic naming algorithm
- Prefix = `AutoLocalize_` - this can be overridden when declaring your
    `[assembly:AutoLocalizeValidationAttributes(typeof(MyResource))]` attribute by
    setting the `ErrorMessageResourceNamePrefix` property.
- ShortAttributeName = The class name of the attribute that descends from `ValidationAttribute`
    without the word `Attribute` at the end.
    - `RequiredAttribute` => `Required`
    - `SomeGenericValidationAttribute<int>` => `SomeGenericValidation`

### Examples
```c#
[assembly:AutoLocalizeValidationAttributes(typeof(DataAnnotationsMessages))]
```

|Attribute|ErrorMessageResourceType|ErrorMessageResourceName|
|---------|------------------------|------------------------|
|`[Required]`|DataAnnotationsMessages|AutoLocalize_Required|
|`[Range(0, int.MaxValue, ErrorMessageResourceName="CannotBeNegative")]`|DataAnnotationsMessages|&lt;unaltered&gt;|
|`[EmailAddress(ErrorMessageResourceType=typeof(X), ErrorMessageResourceName="MustBeAValidEmailAddress")]`|&lt;unaltered&gt;|&lt;unaltered&gt;|
|`[EmailAddress(ErrorMessage="{0} must be a valid email address")]`|&lt;unaltered&gt;|&lt;unaltered&gt;|

```c#
[assembly:AutoLocalizeValidationAttributes(
   typeof(DataAnnotationsMessages),
   ErrorMessageResourceNamePrefix = "ValidationError_"
   )]
```

|Attribute|ErrorMessageResourceType|ErrorMessageResourceName|
|---------|------------------------|------------------------|
|`[Required]`|DataAnnotationsMessages|ValidationError_Required|


## Manifest of resource keys

During weaving a CSV file named after your project (e.g.
`MyProject.Morris.AutoLocalize.ValidationAttributes.csv`) is written next to the project file. It
contains all resource keys the weaver added or discovered:

```
ErrorMessageResourceName
AutoLocalize_Required
AutoLocalize_StringLength
```

Use this manifest to ensure your resource file contains entries for each key.

# Automatic translation
There is a script in the root of this repository named `TranslateLanguage.ps1`.

This script will use ChatGPT API to translate your resx files into
another language. Instructions on how to run it are at the top of that file.
