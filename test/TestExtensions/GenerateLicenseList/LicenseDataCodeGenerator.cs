// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace GenerateLicenseList
{
    internal class LicenseDataCodeGenerator
    {

        private LicenseDataParser _parser;
        public LicenseDataCodeGenerator(string licenseFile, string exceptionsFile)
        {
            _parser = new LicenseDataParser(licenseFile, exceptionsFile);
        }

        private ClassDeclarationSyntax GetLicenseDataHolderClass()
        {
            return CSharpSyntaxTree.ParseText(GenerateLicenseData(_parser))
                   .GetRoot()
                   .DescendantNodes()
                   .OfType<ClassDeclarationSyntax>()
                   .FirstOrDefault();
        }

        public SyntaxNode GenerateLicenseDataFile()
        {
            var rootNode = CSharpSyntaxTree.ParseText(NamespaceDeclaration).GetRoot();

            var nameSpace = rootNode.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();

            if (nameSpace != null)
            {
                var licenseDataHolder = GetLicenseDataHolderClass();
                var licenseDataClass = GetLicenseDataClass();
                var exceptionDataClass = GetExceptionDataClass();

                var newNameSpace = nameSpace.AddMembers(licenseDataHolder, licenseDataClass, exceptionDataClass);
                rootNode = rootNode.ReplaceNode(nameSpace, newNameSpace);
                var workspace = new AdhocWorkspace();
                return Formatter.Format(rootNode, workspace);
            }
            else
            {
                Console.WriteLine("The namespace could not be found.");
                return null;
            }
        }

        private ClassDeclarationSyntax GetLicenseDataClass()
        {
            var licenseDataFormattedClass =
                Environment.NewLine +
                CSharpSyntaxTree.ParseText(LicenseData)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault().NormalizeWhitespace().ToFullString() +
                Environment.NewLine;

            return CSharpSyntaxTree.ParseText(licenseDataFormattedClass)
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault();
        }

        private ClassDeclarationSyntax GetExceptionDataClass()
        {
            var exceptionDataFormattedClass =
                Environment.NewLine +
                CSharpSyntaxTree.ParseText(ExceptionData)
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault().NormalizeWhitespace().ToFullString();

            return CSharpSyntaxTree.ParseText(exceptionDataFormattedClass)
                 .GetRoot()
                 .DescendantNodes()
                 .OfType<ClassDeclarationSyntax>()
                 .FirstOrDefault();
        }

        private string GenerateLicenseData(LicenseDataParser licenseDataParser)
        {
            var licenses = licenseDataParser.ParseLicenses();
            var exceptions = licenseDataParser.ParseExceptions();
            if (!licenses.LicenseListVersion.Equals(exceptions.LicenseListVersion))
            {
                throw new ArgumentException("The license list version and the exception list version are not equivalent");
            }

            return Environment.NewLine +
                LicenseDataClassDeclaration +
                licenses.LicenseListVersion +
                DictionaryDeclaration +
                string.Join(Environment.NewLine, licenses.LicenseList.Select(e => PrettyPrint(e))) +
                ClosingBracket +
                string.Join(Environment.NewLine, exceptions.ExceptionList.Select(e => PrettyPrint(e))) +
                ClosingBracket2 +
                Environment.NewLine;
        }

        private static string PrettyPrint(LicenseData licenseData)
        {
            return $@"            {{ ""{licenseData.LicenseID}"", new LicenseData(licenseID: ""{licenseData.LicenseID}"", referenceNumber: {licenseData.ReferenceNumber}, isOsiApproved: {licenseData.IsOsiApproved.ToString().ToLowerInvariant()}, isDeprecatedLicenseId: {licenseData.IsDeprecatedLicenseId.ToString().ToLowerInvariant()}) }}, ";
        }

        private static string PrettyPrint(ExceptionData exceptionData)
        {
            return $@"            {{ ""{exceptionData.LicenseExceptionID}"", new ExceptionData(licenseID: ""{exceptionData.LicenseExceptionID}"", referenceNumber: {exceptionData.ReferenceNumber}, isDeprecatedLicenseId: {exceptionData.IsDeprecatedLicenseId.ToString().ToLowerInvariant()}) }}, ";
        }

        private static string LicenseDataClassDeclaration = $@" // Auto-Generated by the GenerateLicenseList tool. DO NOT EDIT this manually. Use the update script at $repositoryRoot/scripts/utils/UpdateNuGetLicenseSPDXList.ps1
internal class NuGetLicenseData
{{
    public static string LicenseListVersion = """;

        private static string DictionaryDeclaration = $@""";

    public static readonly IReadOnlyDictionary<string, LicenseData> LicenseList = new Dictionary<string, LicenseData>()
        {{" + Environment.NewLine;

        private static string ClosingBracket = Environment.NewLine + $@"        }};

    public static readonly IReadOnlyDictionary<string, ExceptionData> ExceptionList = new Dictionary<string, ExceptionData>()
        {{" + Environment.NewLine;

        private static string ClosingBracket2 = Environment.NewLine + $@"        }};" + Environment.NewLine + $@"}}";

        private static readonly string LicenseData = $@"internal class LicenseData
{{
    public LicenseData(string licenseID, int referenceNumber, bool isOsiApproved, bool isDeprecatedLicenseId)
    {{
        LicenseID = licenseID;
        ReferenceNumber = referenceNumber;
        IsOsiApproved = isOsiApproved;
        IsDeprecatedLicenseId = isDeprecatedLicenseId;
    }}

    internal string LicenseID {{ get; }}
    internal int ReferenceNumber {{ get; }}
    internal bool IsOsiApproved {{ get; }}
    internal bool IsDeprecatedLicenseId {{ get; }}
}}";

        private static readonly string ExceptionData = $@"internal class ExceptionData
{{
    public ExceptionData(string licenseID, int referenceNumber, bool isDeprecatedLicenseId)
    {{
        LicenseExceptionID = licenseID;
        ReferenceNumber = referenceNumber;
        IsDeprecatedLicenseId = isDeprecatedLicenseId;
    }}

    internal string LicenseExceptionID {{ get; }}
    internal int ReferenceNumber {{ get; }}
    internal bool IsDeprecatedLicenseId {{ get; }}
}}";

        private static readonly string NamespaceDeclaration = $@"// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace NuGet.Packaging.Licenses
{{

}}
";
    }
}
