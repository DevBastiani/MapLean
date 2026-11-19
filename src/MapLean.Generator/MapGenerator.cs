using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MapLean.Generator;

[Generator]
public sealed class MapGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Rule = new(
        MapDiagnostics.Id,
        "Map contract",
        "{0}",
        "MapLean",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var contracts = context.SyntaxProvider.ForAttributeWithMetadataName(
            "MapLean.ToAttribute",
            static (node, _) => node is MethodDeclarationSyntax or TypeDeclarationSyntax,
            static (ctx, _) => ContractReader.Read(ctx))
            .WithTrackingName("Contracts");

        context.RegisterSourceOutput(contracts, static (source, result) =>
        {
            if (result is ContractResult.Bad bad)
            {
                var location = bad.Path.Length == 0
                    ? Location.None
                    : Location.Create(
                        bad.Path,
                        new TextSpan(bad.Start, bad.Length),
                        new LinePositionSpan(
                            new LinePosition(bad.StartLine, bad.StartCharacter),
                            new LinePosition(bad.EndLine, bad.EndCharacter)));
                source.ReportDiagnostic(Diagnostic.Create(Rule, location, bad.Message));
                return;
            }

            var ok = (ContractResult.Ok)result;
            source.AddSource(ok.Hint, SourceText.From(SourceEmitter.Emit(ok.Contract), Encoding.UTF8));
        });
    }
}
