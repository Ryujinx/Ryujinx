using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Ryujinx.HLE.Generators
{
    internal class ServiceSyntaxReceiver : ISyntaxReceiver
    {
        public HashSet<ClassDeclarationSyntax> Types = new HashSet<ClassDeclarationSyntax>();

        public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
        {
            if (syntaxNode is ClassDeclarationSyntax classDeclaration)
            {
                if (classDeclaration.BaseList == null)
                {
                    return;
                }

                var name = classDeclaration.Identifier.ToString();

                Types.Add(classDeclaration);
            }
        }
    }
}
