using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morpeh.SourceGenerator
{
    internal record MarkedClass(INamedTypeSymbol ClassSymbol, INamedTypeSymbol StashType, string StashVariableName);

    internal class MarkedClassKeyComparer : IEqualityComparer<(INamedTypeSymbol ClassSymbol, INamedTypeSymbol StashType, string VariableName)>
    {
        public bool Equals((INamedTypeSymbol ClassSymbol, INamedTypeSymbol StashType, string VariableName) x,
            (INamedTypeSymbol ClassSymbol, INamedTypeSymbol StashType, string VariableName) y)
        {
            return SymbolEqualityComparer.Default.Equals(x.ClassSymbol, y.ClassSymbol) &&
                   SymbolEqualityComparer.Default.Equals(x.StashType, y.StashType) &&
                   x.VariableName == y.VariableName;
        }

        public int GetHashCode((INamedTypeSymbol ClassSymbol, INamedTypeSymbol StashType, string VariableName) obj)
        {
            unchecked
            {
                var hash = SymbolEqualityComparer.Default.GetHashCode(obj.ClassSymbol);
                hash = (hash * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.StashType);
                hash = (hash * 397) ^ (obj.VariableName?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }

    [Generator]
    public class WithStashGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            try
            {
                var classDeclarations = context.SyntaxProvider
                    .CreateSyntaxProvider(
                        predicate: static (s, _) => s is ClassDeclarationSyntax { AttributeLists.Count: > 0 },
                        transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
                    );

                IncrementalValueProvider<(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes)>
                    compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

                context.RegisterSourceOutput(compilationAndClasses,
                    static (spc, source) => Execute(source.compilation, source.classes, spc));
            }
            catch (Exception ex)
            {
                context.RegisterPostInitializationOutput(ctx =>
                    ctx.AddSource($"{nameof(WithStashGenerator)}_Initialization_Error.cs",
                        $"""
                         // Auto-generated code
                         #error Code Generator Initialization Failed: {ex.Message}
                         """));
            }
        }

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes,
            SourceProductionContext context)
        {
            try
            {
                if (classes.IsDefaultOrEmpty)
                {
                    return;
                }

                var markedClasses = GetMarkedClasses(compilation, classes,
                    "Scellecs.Morpeh.SourceGenerator.GetStashAttribute");

                var distinctMarkedClasses = markedClasses
                    .GroupBy(mc => (ClassSymbol: mc.ClassSymbol, StashType: mc.StashType, VariableName: mc.StashVariableName),
                        new MarkedClassKeyComparer())
                    .Select(g => g.First());

                var generatedCount = 0;

                var classGroups = distinctMarkedClasses.GroupBy(mc => mc.ClassSymbol, SymbolEqualityComparer.Default);

                foreach (var classGroup in classGroups)
                {
                    try
                    {
                        var sourceCode = GenerateStashCode(classGroup.ToList());
                        var className = classGroup.Key.Name;
                        context.AddSource($"{className}_Stash.g.cs", sourceCode);
                        generatedCount++;
                    }
                    catch (Exception e)
                    {
                        ReportStashGenerationFailure(context, e, classGroup.First());
                    }
                }

                ReportSuccess(context, generatedCount);
            }
            catch (Exception ex)
            {
                ReportGeneralFailure(context, ex);
            }
        }

        private static IEnumerable<MarkedClass> GetMarkedClasses(Compilation compilation,
            ImmutableArray<ClassDeclarationSyntax> classes, string attributeClassName)
        {
            var typeAttributeSymbol = compilation.GetTypeByMetadataName(attributeClassName);

            if (typeAttributeSymbol == null)
            {
                yield break;
            }

            foreach (var classDeclaration in classes)
            {
                var semanticModel = compilation.GetSemanticModel(classDeclaration.SyntaxTree);

                if (semanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
                {
                    continue;
                }

                if (!InheritsFromSourceGenSystem(classSymbol))
                {
                    continue;
                }

                var attributes = classSymbol.GetAttributes().Where(attr =>
                    SymbolEqualityComparer.Default.Equals(attr.AttributeClass, typeAttributeSymbol));

                foreach (var attribute in attributes)
                {
                    if (attribute.ConstructorArguments.Length < 1)
                    {
                        continue;
                    }

                    if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol stashType)
                    {
                        continue;
                    }

                    var stashName = char.ToLowerInvariant(stashType.Name[0]) + stashType.Name.Substring(1) + "Stash";

                    if (attribute.ConstructorArguments.Length == 2)
                    {
                        if (attribute.ConstructorArguments[1].Value is string overrideName)
                        {
                            if (!string.IsNullOrEmpty(overrideName))
                            {
                                stashName = overrideName;
                            }
                        }

                    }

                    yield return new MarkedClass(classSymbol, stashType, stashName);
                }
            }
        }

        private static bool InheritsFromSourceGenSystem(INamedTypeSymbol classSymbol)
        {
            var baseType = classSymbol.BaseType;

            while (baseType != null)
            {
                if (baseType.Name == "SourceGenInitializer")
                {
                    return true;
                }
                baseType = baseType.BaseType;
            }

            return false;
        }

        private static string GenerateStashCode(List<MarkedClass> markedClasses)
        {
            var firstClass = markedClasses.First();
            var namespaceName = firstClass.ClassSymbol.ContainingNamespace.ToDisplayString();
            var className = firstClass.ClassSymbol.Name;

            var stashFields = string.Join("\n",
                markedClasses.Select(mc =>
                {
                    var stashTypeName = mc.StashType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var variableName = mc.StashVariableName;
                    return $"        private Stash<{stashTypeName}> {variableName};";
                }));

            var stashInitializations = string.Join("\n",
                markedClasses.Select(mc =>
                {
                    var stashTypeName = mc.StashType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    var variableName = mc.StashVariableName;
                    return $"            {variableName} = World.GetStash<{stashTypeName}>();";
                }));

            return $$"""
                     // <auto-generated/>

                     using Scellecs.Morpeh;

                     namespace {{namespaceName}}
                     {
                         public partial class {{className}}
                         {
                     {{stashFields}}


                             protected sealed override void InitializeStashes()
                             {
                     {{stashInitializations}}
                             }
                         }
                     }
                     """;
        }

        private static void ReportSuccess(SourceProductionContext context, int generatedCount)
        {
            var generatorName = nameof(WithStashGenerator);
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: $"{generatorName.ToUpper()}_001",
                    title: $"{generatorName}: Code Generation Success",
                    messageFormat: "Successfully generated stash initialization code for {0} classes",
                    category: "CodeGeneration",
                    DiagnosticSeverity.Info,
                    isEnabledByDefault: true),
                Location.None,
                generatedCount));
        }

        private static void ReportGeneralFailure(SourceProductionContext context, Exception ex)
        {
            var generatorName = nameof(WithStashGenerator);
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: $"{generatorName.ToUpper()}_002",
                    title: $"{generatorName}: General Code Generation Error",
                    messageFormat: "An exception occurred during code generation: {0}",
                    category: "CodeGeneration",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                ex.ToString()));
        }

        private static void ReportStashGenerationFailure(SourceProductionContext context, Exception ex, MarkedClass markedClass)
        {
            var generatorName = nameof(WithStashGenerator);
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: $"{generatorName.ToUpper()}_003",
                    title: $"{generatorName}: Stash Generation Error",
                    messageFormat: "Failed to generate stash code for class {0}: {1}",
                    category: "CodeGeneration",
                    DiagnosticSeverity.Error,
                    isEnabledByDefault: true),
                Location.None,
                markedClass.ClassSymbol.Name,
                ex.Message));
        }
    }
}