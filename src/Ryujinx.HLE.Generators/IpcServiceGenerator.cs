using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;

namespace Ryujinx.HLE.Generators
{
    [Generator]
    public class IpcServiceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            var syntaxReceiver = (ServiceSyntaxReceiver)context.SyntaxReceiver;
            CodeGenerator generator = new CodeGenerator();
            generator = new CodeGenerator();

            generator.AppendLine("using System;");
            generator.EnterScope($"namespace Ryujinx.HLE.HOS.Services.Sm");
            generator.EnterScope($"partial class IUserInterface");

            generator.EnterScope($"public IpcService? GetServiceInstance(Type type, ServiceCtx context, object? parameter = null)");
            foreach (var className in syntaxReceiver.Types)
            {
                if (className.Modifiers.Any(SyntaxKind.AbstractKeyword) || className.Modifiers.Any(SyntaxKind.PrivateKeyword) || !className.AttributeLists.Any(x => x.Attributes.Any(y => y.ToString().StartsWith("Service"))))
                    continue;
                var name = GetFullName(className, context).Replace("global::", "");
                if (!name.StartsWith("Ryujinx.HLE.HOS.Services"))
                    continue;
                var constructors = className.ChildNodes().Where(x => x.IsKind(SyntaxKind.ConstructorDeclaration)).Select(y => y as ConstructorDeclarationSyntax);

                if (!constructors.Any(x => x.ParameterList.Parameters.Count >= 1))
                    continue;

                if (constructors.Where(x => x.ParameterList.Parameters.Count >= 1).FirstOrDefault().ParameterList.Parameters[0].Type.ToString() == "ServiceCtx")
                {
                    generator.EnterScope($"if (type == typeof({GetFullName(className, context)}))");
                    if (constructors.Any(x => x.ParameterList.Parameters.Count == 2))
                    {
                        var type = constructors.Where(x => x.ParameterList.Parameters.Count == 2).FirstOrDefault().ParameterList.Parameters[1].Type;
                        var model = context.Compilation.GetSemanticModel(type.SyntaxTree);
                        var typeSymbol = model.GetSymbolInfo(type).Symbol as INamedTypeSymbol;
                        var fullName = typeSymbol.ToString();
                        generator.EnterScope("if (parameter != null)");
                        generator.AppendLine($"return new {GetFullName(className, context)}(context, ({fullName})parameter);");
                        generator.LeaveScope();
                    }

                    if (constructors.Any(x => x.ParameterList.Parameters.Count == 1))
                    {
                        generator.AppendLine($"return new {GetFullName(className, context)}(context);");
                    }

                    generator.LeaveScope();
                }
            }

            generator.AppendLine("return null;");
            generator.LeaveScope();

            generator.LeaveScope();
            generator.LeaveScope();
            context.AddSource($"IUserInterface.g.cs", generator.ToString());
        }

        private string GetFullName(ClassDeclarationSyntax syntaxNode, GeneratorExecutionContext context)
        {
            var typeSymbol = context.Compilation.GetSemanticModel(syntaxNode.SyntaxTree).GetDeclaredSymbol(syntaxNode);

            return typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new ServiceSyntaxReceiver());
        }
    }
}
