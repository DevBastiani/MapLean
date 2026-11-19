using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MapLean.Generator;

[Generator]
public sealed class MapGenerator : IIncrementalGenerator
{
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
                source.ReportDiagnostic(MapDiagnostics.Create(bad.Problem));
                return;
            }

            var ok = (ContractResult.Ok)result;
            for (var i = 0; i < ok.Warnings.Length; i++)
                source.ReportDiagnostic(MapDiagnostics.Create(ok.Warnings[i]));
            source.AddSource(ok.Hint, SourceText.From(SourceEmitter.Emit(ok.Contract), Encoding.UTF8));
        });
    }
}
