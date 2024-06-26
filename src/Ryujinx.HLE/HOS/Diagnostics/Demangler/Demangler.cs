using Ryujinx.HLE.HOS.Diagnostics.Demangler.Ast;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ryujinx.HLE.HOS.Diagnostics.Demangler
{
    class Demangler
    {
        private const string Base36 = "0123456789abcdefghijklmnopqrstuvwxyz";
        private readonly List<BaseNode> _substitutionList = new();
        private List<BaseNode> _templateParamList = new();

        private readonly List<ForwardTemplateReference> _forwardTemplateReferenceList = new();

        public string Mangled { get; private set; }

        private int _position;
        private readonly int _length;

        private bool _canForwardTemplateReference;
        private bool _canParseTemplateArgs;

        public Demangler(string mangled)
        {
            Mangled = mangled;
            _position = 0;
            _length = mangled.Length;
            _canParseTemplateArgs = true;
        }

        private bool ConsumeIf(string toConsume)
        {
            var mangledPart = Mangled.AsSpan(_position);

            if (mangledPart.StartsWith(toConsume.AsSpan()))
            {
                _position += toConsume.Length;

                return true;
            }

            return false;
        }

        private ReadOnlySpan<char> PeekString(int offset = 0, int length = 1)
        {
            if (_position + offset >= length)
            {
                return null;
            }

            return Mangled.AsSpan(_position + offset, length);
        }

        private char Peek(int offset = 0)
        {
            if (_position + offset >= _length)
            {
                return '\0';
            }

            return Mangled[_position + offset];
        }

        private char Consume()
        {
            if (_position < _length)
            {
                return Mangled[_position++];
            }

            return '\0';
        }

        private int Count()
        {
            return _length - _position;
        }

        private static int FromBase36(string encoded)
        {
            char[] reversedEncoded = encoded.ToLower().ToCharArray().Reverse().ToArray();

            int result = 0;

            for (int i = 0; i < reversedEncoded.Length; i++)
            {
                int value = Base36.IndexOf(reversedEncoded[i]);
                if (value == -1)
                {
                    return -1;
                }

                result += value * (int)Math.Pow(36, i);
            }

            return result;
        }

        private int ParseSeqId()
        {
            ReadOnlySpan<char> part = Mangled.AsSpan(_position);
            int seqIdLen = 0;

            for (; seqIdLen < part.Length; seqIdLen++)
            {
                if (!char.IsLetterOrDigit(part[seqIdLen]))
                {
                    break;
                }
            }

            _position += seqIdLen;

            return FromBase36(new string(part[..seqIdLen]));
        }

        //   <substitution> ::= S <seq-id> _
        //                  ::= S_
        //                  ::= St # std::
        //                  ::= Sa # std::allocator
        //                  ::= Sb # std::basic_string
        //                  ::= Ss # std::basic_string<char, std::char_traits<char>, std::allocator<char> >
        //                  ::= Si # std::basic_istream<char, std::char_traits<char> >
        //                  ::= So # std::basic_ostream<char, std::char_traits<char> >
        //                  ::= Sd # std::basic_iostream<char, std::char_traits<char> >
        private BaseNode ParseSubstitution()
        {
            if (!ConsumeIf("S"))
            {
                return null;
            }

            char substitutionSecondChar = Peek();
            if (char.IsLower(substitutionSecondChar))
            {
                switch (substitutionSecondChar)
                {
                    case 'a':
                        _position++;
                        return new SpecialSubstitution(SpecialSubstitution.SpecialType.Allocator);
                    case 'b':
                        _position++;
                        return new SpecialSubstitution(SpecialSubstitution.SpecialType.BasicString);
                    case 's':
                        _position++;
                        return new SpecialSubstitution(SpecialSubstitution.SpecialType.String);
                    case 'i':
                        _position++;
                        return new SpecialSubstitution(SpecialSubstitution.SpecialType.IStream);
                    case 'o':
                        _position++;
                        return new SpecialSubstitution(SpecialSubstitution.SpecialType.OStream);
                    case 'd':
                        _position++;
                        return new SpecialSubstitution(SpecialSubstitution.SpecialType.IOStream);
                    default:
                        return null;
                }
            }

            // ::= S_
            if (ConsumeIf("_"))
            {
                if (_substitutionList.Count != 0)
                {
                    return _substitutionList[0];
                }

                return null;
            }

            //                ::= S <seq-id> _
            int seqId = ParseSeqId();
            if (seqId < 0)
            {
                return null;
            }

            seqId++;

            if (!ConsumeIf("_") || seqId >= _substitutionList.Count)
            {
                return null;
            }

            return _substitutionList[seqId];
        }

        // NOTE: thoses data aren't used in the output
        //  <call-offset> ::= h <nv-offset> _
        //                ::= v <v-offset> _
        //  <nv-offset>   ::= <offset number>
        //                    # non-virtual base override
        //  <v-offset>    ::= <offset number> _ <virtual offset number>
        //                    # virtual base override, with vcall offset
        private bool ParseCallOffset()
        {
            if (ConsumeIf("h"))
            {
                return ParseNumber(true).Length == 0 || !ConsumeIf("_");
            }
            else if (ConsumeIf("v"))
            {
                return ParseNumber(true).Length == 0 || !ConsumeIf("_") || ParseNumber(true).Length == 0 || !ConsumeIf("_");
            }

            return true;
        }


        //   <class-enum-type> ::= <name>     # non-dependent type name, dependent type name, or dependent typename-specifier
        //                     ::= Ts <name>  # dependent elaborated type specifier using 'struct' or 'class'
        //                     ::= Tu <name>  # dependent elaborated type specifier using 'union'
        //                     ::= Te <name>  # dependent elaborated type specifier using 'enum'
        private BaseNode ParseClassEnumType()
        {
            string elaboratedType = null;

            if (ConsumeIf("Ts"))
            {
                elaboratedType = "struct";
            }
            else if (ConsumeIf("Tu"))
            {
                elaboratedType = "union";
            }
            else if (ConsumeIf("Te"))
            {
                elaboratedType = "enum";
            }

            BaseNode name = ParseName();
            if (name == null)
            {
                return null;
            }

            if (elaboratedType == null)
            {
                return name;
            }

            return new ElaboratedType(elaboratedType, name);
        }

        //  <function-type>         ::= [<CV-qualifiers>] [<exception-spec>] [Dx] F [Y] <bare-function-type> [<ref-qualifier>] E
        //  <bare-function-type>    ::= <signature type>+
        //                              # types are possible return type, then parameter types
        //  <exception-spec>        ::= Do                # non-throwing exception-specification (e.g., noexcept, throw())
        //                          ::= DO <expression> E # computed (instantiation-dependent) noexcept
        //                          ::= Dw <type>+ E      # dynamic exception specification with instantiation-dependent types
        private FunctionType ParseFunctionType()
        {
            Cv cvQualifiers = ParseCvQualifiers();

            BaseNode exceptionSpec = null;

            if (ConsumeIf("Do"))
            {
                exceptionSpec = new NameType("noexcept");
            }
            else if (ConsumeIf("DO"))
            {
                BaseNode expression = ParseExpression();
                if (expression == null || !ConsumeIf("E"))
                {
                    return null;
                }

                exceptionSpec = new NoexceptSpec(expression);
            }
            else if (ConsumeIf("Dw"))
            {
                List<BaseNode> types = new();

                while (!ConsumeIf("E"))
                {
                    BaseNode type = ParseType();
                    if (type == null)
                    {
                        return null;
                    }

                    types.Add(type);
                }

                exceptionSpec = new DynamicExceptionSpec(new NodeArray(types));
            }

            // We don't need the transaction
            ConsumeIf("Dx");

            if (!ConsumeIf("F"))
            {
                return null;
            }

            // extern "C"
            ConsumeIf("Y");

            BaseNode returnType = ParseType();
            if (returnType == null)
            {
                return null;
            }

            Reference referenceQualifier = Reference.None;
            List<BaseNode> paramsList = new();

            while (true)
            {
                if (ConsumeIf("E"))
                {
                    break;
                }

                if (ConsumeIf("v"))
                {
                    continue;
                }

                if (ConsumeIf("RE"))
                {
                    referenceQualifier = Reference.LValue;
                    break;
                }
                else if (ConsumeIf("OE"))
                {
                    referenceQualifier = Reference.RValue;
                    break;
                }

                BaseNode type = ParseType();
                if (type == null)
                {
                    return null;
                }

                paramsList.Add(type);
            }

            return new FunctionType(returnType, new NodeArray(paramsList), new CvType(cvQualifiers, null), new SimpleReferenceType(referenceQualifier, null), exceptionSpec);
        }

        //   <array-type> ::= A <positive dimension number> _ <element type>
        //                ::= A [<dimension expression>] _ <element type>
        private ArrayType ParseArrayType()
        {
            if (!ConsumeIf("A"))
            {
                return null;
            }

            BaseNode elementType;
            if (char.IsDigit(Peek()))
            {
                string dimension = ParseNumber();
                if (dimension.Length == 0 || !ConsumeIf("_"))
                {
                    return null;
                }

                elementType = ParseType();
                if (elementType == null)
                {
                    return null;
                }

                return new ArrayType(elementType, dimension);
            }

            if (!ConsumeIf("_"))
            {
                BaseNode dimensionExpression = ParseExpression();
                if (dimensionExpression == null || !ConsumeIf("_"))
                {
                    return null;
                }

                elementType = ParseType();
                if (elementType == null)
                {
                    return null;
                }

                return new ArrayType(elementType, dimensionExpression);
            }

            elementType = ParseType();
            if (elementType == null)
            {
                return null;
            }

            return new ArrayType(elementType);
        }

        // <type>  ::= <builtin-type>
        //         ::= <qualified-type> (PARTIAL)
        //         ::= <function-type>
        //         ::= <class-enum-type>
        //         ::= <array-type> (TODO)
        //         ::= <pointer-to-member-type> (TODO)
        //         ::= <template-param>
        //         ::= <template-template-param> <template-args>
        //         ::= <decltype>
        //         ::= P <type>        # pointer
        //         ::= R <type>        # l-value reference
        //         ::= O <type>        # r-value reference (C++11)
        //         ::= C <type>        # complex pair (C99)
        //         ::= G <type>        # imaginary (C99)
        //         ::= <substitution>  # See Compression below
        private BaseNode ParseType(NameParserContext context = null)
        {
            // Temporary context
            context ??= new NameParserContext();

            BaseNode result;
            switch (Peek())
            {
                case 'r':
                case 'V':
                case 'K':
                    int typePos = 0;

                    if (Peek(typePos) == 'r')
                    {
                        typePos++;
                    }

                    if (Peek(typePos) == 'V')
                    {
                        typePos++;
                    }

                    if (Peek(typePos) == 'K')
                    {
                        typePos++;
                    }

                    if (Peek(typePos) == 'F' || (Peek(typePos) == 'D' && (Peek(typePos + 1) == 'o' || Peek(typePos + 1) == 'O' || Peek(typePos + 1) == 'w' || Peek(typePos + 1) == 'x')))
                    {
                        result = ParseFunctionType();
                        break;
                    }

                    Cv cv = ParseCvQualifiers();

                    result = ParseType(context);

                    if (result == null)
                    {
                        return null;
                    }

                    result = new CvType(cv, result);
                    break;
                case 'U':
                    // TODO: <extended-qualifier>
                    return null;
                case 'v':
                    _position++;
                    return new NameType("void");
                case 'w':
                    _position++;
                    return new NameType("wchar_t");
                case 'b':
                    _position++;
                    return new NameType("bool");
                case 'c':
                    _position++;
                    return new NameType("char");
                case 'a':
                    _position++;
                    return new NameType("signed char");
                case 'h':
                    _position++;
                    return new NameType("unsigned char");
                case 's':
                    _position++;
                    return new NameType("short");
                case 't':
                    _position++;
                    return new NameType("unsigned short");
                case 'i':
                    _position++;
                    return new NameType("int");
                case 'j':
                    _position++;
                    return new NameType("unsigned int");
                case 'l':
                    _position++;
                    return new NameType("long");
                case 'm':
                    _position++;
                    return new NameType("unsigned long");
                case 'x':
                    _position++;
                    return new NameType("long long");
                case 'y':
                    _position++;
                    return new NameType("unsigned long long");
                case 'n':
                    _position++;
                    return new NameType("__int128");
                case 'o':
                    _position++;
                    return new NameType("unsigned __int128");
                case 'f':
                    _position++;
                    return new NameType("float");
                case 'd':
                    _position++;
                    return new NameType("double");
                case 'e':
                    _position++;
                    return new NameType("long double");
                case 'g':
                    _position++;
                    return new NameType("__float128");
                case 'z':
                    _position++;
                    return new NameType("...");
                case 'u':
                    _position++;
                    return ParseSourceName();
                case 'D':
                    switch (Peek(1))
                    {
                        case 'd':
                            _position += 2;
                            return new NameType("decimal64");
                        case 'e':
                            _position += 2;
                            return new NameType("decimal128");
                        case 'f':
                            _position += 2;
                            return new NameType("decimal32");
                        case 'h':
                            _position += 2;
                            // FIXME: GNU c++flit returns this but that is not what is supposed to be returned.
                            return new NameType("half"); // return new NameType("decimal16");
                        case 'i':
                            _position += 2;
                            return new NameType("char32_t");
                        case 's':
                            _position += 2;
                            return new NameType("char16_t");
                        case 'a':
                            _position += 2;
                            return new NameType("decltype(auto)");
                        case 'n':
                            _position += 2;
                            // FIXME: GNU c++flit returns this but that is not what is supposed to be returned.
                            return new NameType("decltype(nullptr)"); // return new NameType("std::nullptr_t");
                        case 't':
                        case 'T':
                            _position += 2;
                            result = ParseDecltype();
                            break;
                        case 'o':
                        case 'O':
                        case 'w':
                        case 'x':
                            result = ParseFunctionType();
                            break;
                        default:
                            return null;
                    }
                    break;
                case 'F':
                    result = ParseFunctionType();
                    break;
                case 'A':
                    return ParseArrayType();
                case 'M':
                    // TODO: <pointer-to-member-type>
                    _position++;
                    return null;
                case 'T':
                    // might just be a class enum type
                    if (Peek(1) == 's' || Peek(1) == 'u' || Peek(1) == 'e')
                    {
                        result = ParseClassEnumType();
                        break;
                    }

                    result = ParseTemplateParam();
                    if (result == null)
                    {
                        return null;
                    }

                    if (_canParseTemplateArgs && Peek() == 'I')
                    {
                        BaseNode templateArguments = ParseTemplateArguments();
                        if (templateArguments == null)
                        {
                            return null;
                        }

                        result = new NameTypeWithTemplateArguments(result, templateArguments);
                    }
                    break;
                case 'P':
                    _position++;
                    result = ParseType(context);

                    if (result == null)
                    {
                        return null;
                    }

                    result = new PointerType(result);
                    break;
                case 'R':
                    _position++;
                    result = ParseType(context);

                    if (result == null)
                    {
                        return null;
                    }

                    result = new ReferenceType("&", result);
                    break;
                case 'O':
                    _position++;
                    result = ParseType(context);

                    if (result == null)
                    {
                        return null;
                    }

                    result = new ReferenceType("&&", result);
                    break;
                case 'C':
                    _position++;
                    result = ParseType(context);

                    if (result == null)
                    {
                        return null;
                    }

                    result = new PostfixQualifiedType(" complex", result);
                    break;
                case 'G':
                    _position++;
                    result = ParseType(context);

                    if (result == null)
                    {
                        return null;
                    }

                    result = new PostfixQualifiedType(" imaginary", result);
                    break;
                case 'S':
                    if (Peek(1) != 't')
                    {
                        BaseNode substitution = ParseSubstitution();
                        if (substitution == null)
                        {
                            return null;
                        }

                        if (_canParseTemplateArgs && Peek() == 'I')
                        {
                            BaseNode templateArgument = ParseTemplateArgument();
                            if (templateArgument == null)
                            {
                                return null;
                            }

                            result = new NameTypeWithTemplateArguments(substitution, templateArgument);
                            break;
                        }
                        return substitution;
                    }
                    else
                    {
                        result = ParseClassEnumType();
                        break;
                    }
                default:
                    result = ParseClassEnumType();
                    break;
            }
            if (result != null)
            {
                _substitutionList.Add(result);
            }

            return result;
        }

        // <special-name> ::= TV <type> # virtual table
        //                ::= TT <type> # VTT structure (construction vtable index)
        //                ::= TI <type> # typeinfo structure
        //                ::= TS <type> # typeinfo name (null-terminated byte string)
        //                ::= Tc <call-offset> <call-offset> <base encoding>
        //                ::= TW <object name> # Thread-local wrapper
        //                ::= TH <object name> # Thread-local initialization
        //                ::= T <call-offset> <base encoding>
        //                              # base is the nominal target function of thunk
        //                ::= GV <object name>	# Guard variable for one-time initialization
        private BaseNode ParseSpecialName(NameParserContext context = null)
        {
            if (Peek() != 'T')
            {
                if (ConsumeIf("GV"))
                {
                    BaseNode name = ParseName();
                    if (name == null)
                    {
                        return null;
                    }

                    return new SpecialName("guard variable for ", name);
                }
                return null;
            }

            BaseNode node;
            switch (Peek(1))
            {
                // ::= TV <type>    # virtual table
                case 'V':
                    _position += 2;
                    node = ParseType(context);
                    if (node == null)
                    {
                        return null;
                    }

                    return new SpecialName("vtable for ", node);
                // ::= TT <type>    # VTT structure (construction vtable index)
                case 'T':
                    _position += 2;
                    node = ParseType(context);
                    if (node == null)
                    {
                        return null;
                    }

                    return new SpecialName("VTT for ", node);
                // ::= TI <type>    # typeinfo structure
                case 'I':
                    _position += 2;
                    node = ParseType(context);
                    if (node == null)
                    {
                        return null;
                    }

                    return new SpecialName("typeinfo for ", node);
                // ::= TS <type> # typeinfo name (null-terminated byte string)
                case 'S':
                    _position += 2;
                    node = ParseType(context);
                    if (node == null)
                    {
                        return null;
                    }

                    return new SpecialName("typeinfo name for ", node);
                // ::= Tc <call-offset> <call-offset> <base encoding>
                case 'c':
                    _position += 2;
                    if (ParseCallOffset() || ParseCallOffset())
                    {
                        return null;
                    }

                    node = ParseEncoding();
                    if (node == null)
                    {
                        return null;
                    }

                    return new SpecialName("covariant return thunk to ", node);
                // extension ::= TC <first type> <number> _ <second type>
                case 'C':
                    _position += 2;
                    BaseNode firstType = ParseType();
                    if (firstType == null || ParseNumber(true).Length == 0 || !ConsumeIf("_"))
                    {
                        return null;
                    }

                    BaseNode secondType = ParseType();

                    return new CtorVtableSpecialName(secondType, firstType);
                // ::= TH <object name> # Thread-local initialization
                case 'H':
                    _position += 2;
                    node = ParseName();
                    if (node == null)
                    {
                        return null;
                    }

                    return new SpecialName("thread-local initialization routine for ", node);
                // ::= TW <object name> # Thread-local wrapper
                case 'W':
                    _position += 2;
                    node = ParseName();
                    if (node == null)
                    {
                        return null;
                    }

                    return new SpecialName("thread-local wrapper routine for ", node);
                default:
                    _position++;
                    bool isVirtual = Peek() == 'v';
                    if (ParseCallOffset())
                    {
                        return null;
                    }

                    node = ParseEncoding();
                    if (node == null)
                    {
                        return null;
                    }

                    if (isVirtual)
                    {
                        return new SpecialName("virtual thunk to ", node);
                    }

                    return new SpecialName("non-virtual thunk to ", node);
            }
        }

        // <CV-qualifiers>      ::= [r] [V] [K] # restrict (C99), volatile, const
        private Cv ParseCvQualifiers()
        {
            Cv qualifiers = Cv.None;

            if (ConsumeIf("r"))
            {
                qualifiers |= Cv.Restricted;
            }
            if (ConsumeIf("V"))
            {
                qualifiers |= Cv.Volatile;
            }
            if (ConsumeIf("K"))
            {
                qualifiers |= Cv.Const;
            }

            return qualifiers;
        }


        // <ref-qualifier>      ::= R              # & ref-qualifier
        // <ref-qualifier>      ::= O              # && ref-qualifier
        private SimpleReferenceType ParseRefQualifiers()
        {
            Reference result = Reference.None;
            if (ConsumeIf("O"))
            {
                result = Reference.RValue;
            }
            else if (ConsumeIf("R"))
            {
                result = Reference.LValue;
            }
            return new SimpleReferenceType(result, null);
        }

        private static BaseNode CreateNameNode(BaseNode prev, BaseNode name, NameParserContext context)
        {
            BaseNode result = name;
            if (prev != null)
            {
                result = new NestedName(name, prev);
            }

            if (context != null)
            {
                context.FinishWithTemplateArguments = false;
            }

            return result;
        }

        private int ParsePositiveNumber()
        {
            ReadOnlySpan<char> part = Mangled.AsSpan(_position);
            int numberLength = 0;

            for (; numberLength < part.Length; numberLength++)
            {
                if (!char.IsDigit(part[numberLength]))
                {
                    break;
                }
            }

            _position += numberLength;

            if (numberLength == 0)
            {
                return -1;
            }

            return int.Parse(part[..numberLength]);
        }

        private string ParseNumber(bool isSigned = false)
        {
            if (isSigned)
            {
                ConsumeIf("n");
            }

            if (Count() == 0 || !char.IsDigit(Mangled[_position]))
            {
                return null;
            }

            ReadOnlySpan<char> part = Mangled.AsSpan(_position);
            int numberLength = 0;

            for (; numberLength < part.Length; numberLength++)
            {
                if (!char.IsDigit(part[numberLength]))
                {
                    break;
                }
            }

            _position += numberLength;

            return new string(part[..numberLength]);
        }

        // <source-name> ::= <positive length number> <identifier>
        private NameType ParseSourceName()
        {
            int length = ParsePositiveNumber();
            if (Count() < length || length <= 0)
            {
                return null;
            }

            string name = Mangled.Substring(_position, length);
            _position += length;
            if (name.StartsWith("_GLOBAL__N"))
            {
                return new NameType("(anonymous namespace)");
            }

            return new NameType(name);
        }

        // <operator-name> ::= nw    # new
        //                 ::= na    # new[]
        //                 ::= dl    # delete
        //                 ::= da    # delete[]
        //                 ::= ps    # + (unary)
        //                 ::= ng    # - (unary)
        //                 ::= ad    # & (unary)
        //                 ::= de    # * (unary)
        //                 ::= co    # ~
        //                 ::= pl    # +
        //                 ::= mi    # -
        //                 ::= ml    # *
        //                 ::= dv    # /
        //                 ::= rm    # %
        //                 ::= an    # &
        //                 ::= or    # |
        //                 ::= eo    # ^
        //                 ::= aS    # =
        //                 ::= pL    # +=
        //                 ::= mI    # -=
        //                 ::= mL    # *=
        //                 ::= dV    # /=
        //                 ::= rM    # %=
        //                 ::= aN    # &=
        //                 ::= oR    # |=
        //                 ::= eO    # ^=
        //                 ::= ls    # <<
        //                 ::= rs    # >>
        //                 ::= lS    # <<=
        //                 ::= rS    # >>=
        //                 ::= eq    # ==
        //                 ::= ne    # !=
        //                 ::= lt    # <
        //                 ::= gt    # >
        //                 ::= le    # <=
        //                 ::= ge    # >=
        //                 ::= ss    # <=>
        //                 ::= nt    # !
        //                 ::= aa    # &&
        //                 ::= oo    # ||
        //                 ::= pp    # ++ (postfix in <expression> context)
        //                 ::= mm    # -- (postfix in <expression> context)
        //                 ::= cm    # ,
        //                 ::= pm    # ->*
        //                 ::= pt    # ->
        //                 ::= cl    # ()
        //                 ::= ix    # []
        //                 ::= qu    # ?
        //                 ::= cv <type>    # (cast) (TODO)
        //                 ::= li <source-name>          # operator ""
        //                 ::= v <digit> <source-name>    # vendor extended operator (TODO)
        private BaseNode ParseOperatorName(NameParserContext context)
        {
            switch (Peek())
            {
                case 'a':
                    switch (Peek(1))
                    {
                        case 'a':
                            _position += 2;
                            return new NameType("operator&&");
                        case 'd':
                        case 'n':
                            _position += 2;
                            return new NameType("operator&");
                        case 'N':
                            _position += 2;
                            return new NameType("operator&=");
                        case 'S':
                            _position += 2;
                            return new NameType("operator=");
                        default:
                            return null;
                    }
                case 'c':
                    switch (Peek(1))
                    {
                        case 'l':
                            _position += 2;
                            return new NameType("operator()");
                        case 'm':
                            _position += 2;
                            return new NameType("operator,");
                        case 'o':
                            _position += 2;
                            return new NameType("operator~");
                        case 'v':
                            _position += 2;

                            bool canParseTemplateArgsBackup = _canParseTemplateArgs;
                            bool canForwardTemplateReferenceBackup = _canForwardTemplateReference;

                            _canParseTemplateArgs = false;
                            _canForwardTemplateReference = canForwardTemplateReferenceBackup || context != null;

                            BaseNode type = ParseType();

                            _canParseTemplateArgs = canParseTemplateArgsBackup;
                            _canForwardTemplateReference = canForwardTemplateReferenceBackup;

                            if (type == null)
                            {
                                return null;
                            }

                            if (context != null)
                            {
                                context.CtorDtorConversion = true;
                            }

                            return new ConversionOperatorType(type);
                        default:
                            return null;
                    }
                case 'd':
                    switch (Peek(1))
                    {
                        case 'a':
                            _position += 2;
                            return new NameType("operator delete[]");
                        case 'e':
                            _position += 2;
                            return new NameType("operator*");
                        case 'l':
                            _position += 2;
                            return new NameType("operator delete");
                        case 'v':
                            _position += 2;
                            return new NameType("operator/");
                        case 'V':
                            _position += 2;
                            return new NameType("operator/=");
                        default:
                            return null;
                    }
                case 'e':
                    switch (Peek(1))
                    {
                        case 'o':
                            _position += 2;
                            return new NameType("operator^");
                        case 'O':
                            _position += 2;
                            return new NameType("operator^=");
                        case 'q':
                            _position += 2;
                            return new NameType("operator==");
                        default:
                            return null;
                    }
                case 'g':
                    switch (Peek(1))
                    {
                        case 'e':
                            _position += 2;
                            return new NameType("operator>=");
                        case 't':
                            _position += 2;
                            return new NameType("operator>");
                        default:
                            return null;
                    }
                case 'i':
                    if (Peek(1) == 'x')
                    {
                        _position += 2;
                        return new NameType("operator[]");
                    }
                    return null;
                case 'l':
                    switch (Peek(1))
                    {
                        case 'e':
                            _position += 2;
                            return new NameType("operator<=");
                        case 'i':
                            _position += 2;
                            BaseNode sourceName = ParseSourceName();
                            if (sourceName == null)
                            {
                                return null;
                            }

                            return new LiteralOperator(sourceName);
                        case 's':
                            _position += 2;
                            return new NameType("operator<<");
                        case 'S':
                            _position += 2;
                            return new NameType("operator<<=");
                        case 't':
                            _position += 2;
                            return new NameType("operator<");
                        default:
                            return null;
                    }
                case 'm':
                    switch (Peek(1))
                    {
                        case 'i':
                            _position += 2;
                            return new NameType("operator-");
                        case 'I':
                            _position += 2;
                            return new NameType("operator-=");
                        case 'l':
                            _position += 2;
                            return new NameType("operator*");
                        case 'L':
                            _position += 2;
                            return new NameType("operator*=");
                        case 'm':
                            _position += 2;
                            return new NameType("operator--");
                        default:
                            return null;
                    }
                case 'n':
                    switch (Peek(1))
                    {
                        case 'a':
                            _position += 2;
                            return new NameType("operator new[]");
                        case 'e':
                            _position += 2;
                            return new NameType("operator!=");
                        case 'g':
                            _position += 2;
                            return new NameType("operator-");
                        case 't':
                            _position += 2;
                            return new NameType("operator!");
                        case 'w':
                            _position += 2;
                            return new NameType("operator new");
                        default:
                            return null;
                    }
                case 'o':
                    switch (Peek(1))
                    {
                        case 'o':
                            _position += 2;
                            return new NameType("operator||");
                        case 'r':
                            _position += 2;
                            return new NameType("operator|");
                        case 'R':
                            _position += 2;
                            return new NameType("operator|=");
                        default:
                            return null;
                    }
                case 'p':
                    switch (Peek(1))
                    {
                        case 'm':
                            _position += 2;
                            return new NameType("operator->*");
                        case 's':
                        case 'l':
                            _position += 2;
                            return new NameType("operator+");
                        case 'L':
                            _position += 2;
                            return new NameType("operator+=");
                        case 'p':
                            _position += 2;
                            return new NameType("operator++");
                        case 't':
                            _position += 2;
                            return new NameType("operator->");
                        default:
                            return null;
                    }
                case 'q':
                    if (Peek(1) == 'u')
                    {
                        _position += 2;
                        return new NameType("operator?");
                    }
                    return null;
                case 'r':
                    switch (Peek(1))
                    {
                        case 'm':
                            _position += 2;
                            return new NameType("operator%");
                        case 'M':
                            _position += 2;
                            return new NameType("operator%=");
                        case 's':
                            _position += 2;
                            return new NameType("operator>>");
                        case 'S':
                            _position += 2;
                            return new NameType("operator>>=");
                        default:
                            return null;
                    }
                case 's':
                    if (Peek(1) == 's')
                    {
                        _position += 2;
                        return new NameType("operator<=>");
                    }
                    return null;
                case 'v':
                    // TODO: ::= v <digit> <source-name>    # vendor extended operator
                    return null;
                default:
                    return null;
            }
        }

        // <unqualified-name> ::= <operator-name> [<abi-tags> (TODO)]
        //                    ::= <ctor-dtor-name> (TODO)
        //                    ::= <source-name>
        //                    ::= <unnamed-type-name> (TODO)
        //                    ::= DC <source-name>+ E      # structured binding declaration (TODO)
        private BaseNode ParseUnqualifiedName(NameParserContext context)
        {
            BaseNode result = null;
            char c = Peek();
            if (c == 'U')
            {
                // TODO: Unnamed Type Name
                // throw new Exception("Unnamed Type Name not implemented");
            }
            else if (char.IsDigit(c))
            {
                result = ParseSourceName();
            }
            else if (ConsumeIf("DC"))
            {
                // TODO: Structured Binding Declaration
                // throw new Exception("Structured Binding Declaration not implemented");
            }
            else
            {
                result = ParseOperatorName(context);
            }

            if (result != null)
            {
                // TODO: ABI Tags
                // throw new Exception("ABI Tags not implemented");
            }
            return result;
        }

        // <ctor-dtor-name> ::= C1  # complete object constructor
        //                  ::= C2  # base object constructor
        //                  ::= C3  # complete object allocating constructor
        //                  ::= D0  # deleting destructor
        //                  ::= D1  # complete object destructor
        //                  ::= D2  # base object destructor
        private CtorDtorNameType ParseCtorDtorName(NameParserContext context, BaseNode prev)
        {
            if (prev.Type == NodeType.SpecialSubstitution && prev is SpecialSubstitution substitution)
            {
                substitution.SetExtended();
            }

            if (ConsumeIf("C"))
            {
                bool isInherited = ConsumeIf("I");

                char ctorDtorType = Peek();
                if (ctorDtorType != '1' && ctorDtorType != '2' && ctorDtorType != '3')
                {
                    return null;
                }

                _position++;

                if (context != null)
                {
                    context.CtorDtorConversion = true;
                }

                if (isInherited && ParseName(context) == null)
                {
                    return null;
                }

                return new CtorDtorNameType(prev, false);
            }

            if (ConsumeIf("D"))
            {
                char c = Peek();
                if (c != '0' && c != '1' && c != '2')
                {
                    return null;
                }

                _position++;

                if (context != null)
                {
                    context.CtorDtorConversion = true;
                }

                return new CtorDtorNameType(prev, true);
            }

            return null;
        }

        // <function-param> ::= fp <top-level CV-qualifiers> _                                                                                           # L == 0, first parameter
        //                  ::= fp <top-level CV-qualifiers> <parameter-2 non-negative number> _                                                         # L == 0, second and later parameters
        //                  ::= fL <L-1 non-negative number> p <top-level CV-qualifiers> _                                                               # L > 0, first parameter
        //                  ::= fL <L-1 non-negative number> p <top-level CV-qualifiers> <parameter-2 non-negative number> _                             # L > 0, second and later parameters
        private FunctionParameter ParseFunctionParameter()
        {
            if (ConsumeIf("fp"))
            {
                // ignored
                ParseCvQualifiers();

                if (!ConsumeIf("_"))
                {
                    return null;
                }

                return new FunctionParameter(ParseNumber());
            }
            else if (ConsumeIf("fL"))
            {
                string l1Number = ParseNumber();
                if (l1Number == null || l1Number.Length == 0)
                {
                    return null;
                }

                if (!ConsumeIf("p"))
                {
                    return null;
                }

                // ignored
                ParseCvQualifiers();

                if (!ConsumeIf("_"))
                {
                    return null;
                }

                return new FunctionParameter(ParseNumber());
            }

            return null;
        }

        // <fold-expr> ::= fL <binary-operator-name> <expression> <expression>
        //             ::= fR <binary-operator-name> <expression> <expression>
        //             ::= fl <binary-operator-name> <expression>
        //             ::= fr <binary-operator-name> <expression>
        private FoldExpression ParseFoldExpression()
        {
            if (!ConsumeIf("f"))
            {
                return null;
            }

            char foldKind = Peek();
            bool hasInitializer = foldKind == 'L' || foldKind == 'R';
            bool isLeftFold = foldKind == 'l' || foldKind == 'L';

            if (!isLeftFold && !(foldKind == 'r' || foldKind == 'R'))
            {
                return null;
            }

            _position++;

            string operatorName;

            switch (PeekString(0, 2))
            {
                case "aa":
                    operatorName = "&&";
                    break;
                case "an":
                    operatorName = "&";
                    break;
                case "aN":
                    operatorName = "&=";
                    break;
                case "aS":
                    operatorName = "=";
                    break;
                case "cm":
                    operatorName = ",";
                    break;
                case "ds":
                    operatorName = ".*";
                    break;
                case "dv":
                    operatorName = "/";
                    break;
                case "dV":
                    operatorName = "/=";
                    break;
                case "eo":
                    operatorName = "^";
                    break;
                case "eO":
                    operatorName = "^=";
                    break;
                case "eq":
                    operatorName = "==";
                    break;
                case "ge":
                    operatorName = ">=";
                    break;
                case "gt":
                    operatorName = ">";
                    break;
                case "le":
                    operatorName = "<=";
                    break;
                case "ls":
                    operatorName = "<<";
                    break;
                case "lS":
                    operatorName = "<<=";
                    break;
                case "lt":
                    operatorName = "<";
                    break;
                case "mi":
                    operatorName = "-";
                    break;
                case "mI":
                    operatorName = "-=";
                    break;
                case "ml":
                    operatorName = "*";
                    break;
                case "mL":
                    operatorName = "*=";
                    break;
                case "ne":
                    operatorName = "!=";
                    break;
                case "oo":
                    operatorName = "||";
                    break;
                case "or":
                    operatorName = "|";
                    break;
                case "oR":
                    operatorName = "|=";
                    break;
                case "pl":
                    operatorName = "+";
                    break;
                case "pL":
                    operatorName = "+=";
                    break;
                case "rm":
                    operatorName = "%";
                    break;
                case "rM":
                    operatorName = "%=";
                    break;
                case "rs":
                    operatorName = ">>";
                    break;
                case "rS":
                    operatorName = ">>=";
                    break;
                default:
                    return null;
            }

            _position += 2;

            BaseNode expression = ParseExpression();
            if (expression == null)
            {
                return null;
            }

            BaseNode initializer = null;

            if (hasInitializer)
            {
                initializer = ParseExpression();
                if (initializer == null)
                {
                    return null;
                }
            }

            if (isLeftFold && initializer != null)
            {
                (initializer, expression) = (expression, initializer);
            }

            return new FoldExpression(isLeftFold, operatorName, new PackedTemplateParameterExpansion(expression), initializer);
        }


        //                ::= cv <type> <expression>                               # type (expression), conversion with one argument
        //                ::= cv <type> _ <expression>* E                          # type (expr-list), conversion with other than one argument
        private ConversionExpression ParseConversionExpression()
        {
            if (!ConsumeIf("cv"))
            {
                return null;
            }

            bool canParseTemplateArgsBackup = _canParseTemplateArgs;
            _canParseTemplateArgs = false;
            BaseNode type = ParseType();
            _canParseTemplateArgs = canParseTemplateArgsBackup;

            if (type == null)
            {
                return null;
            }

            List<BaseNode> expressions = new();
            if (ConsumeIf("_"))
            {
                while (!ConsumeIf("E"))
                {
                    BaseNode expression = ParseExpression();
                    if (expression == null)
                    {
                        return null;
                    }

                    expressions.Add(expression);
                }
            }
            else
            {
                BaseNode expression = ParseExpression();
                if (expression == null)
                {
                    return null;
                }

                expressions.Add(expression);
            }

            return new ConversionExpression(type, new NodeArray(expressions));
        }

        private BinaryExpression ParseBinaryExpression(string name)
        {
            BaseNode leftPart = ParseExpression();
            if (leftPart == null)
            {
                return null;
            }

            BaseNode rightPart = ParseExpression();
            if (rightPart == null)
            {
                return null;
            }

            return new BinaryExpression(leftPart, name, rightPart);
        }

        private PrefixExpression ParsePrefixExpression(string name)
        {
            BaseNode expression = ParseExpression();
            if (expression == null)
            {
                return null;
            }

            return new PrefixExpression(name, expression);
        }


        // <braced-expression> ::= <expression>
        //                     ::= di <field source-name> <braced-expression>    # .name = expr
        //                     ::= dx <index expression> <braced-expression>     # [expr] = expr
        //                     ::= dX <range begin expression> <range end expression> <braced-expression>
        //                                                                       # [expr ... expr] = expr
        private BaseNode ParseBracedExpression()
        {
            if (Peek() == 'd')
            {
                BaseNode bracedExpressionNode;
                switch (Peek(1))
                {
                    case 'i':
                        _position += 2;
                        BaseNode field = ParseSourceName();
                        if (field == null)
                        {
                            return null;
                        }

                        bracedExpressionNode = ParseBracedExpression();
                        if (bracedExpressionNode == null)
                        {
                            return null;
                        }

                        return new BracedExpression(field, bracedExpressionNode, false);
                    case 'x':
                        _position += 2;
                        BaseNode index = ParseExpression();
                        if (index == null)
                        {
                            return null;
                        }

                        bracedExpressionNode = ParseBracedExpression();
                        if (bracedExpressionNode == null)
                        {
                            return null;
                        }

                        return new BracedExpression(index, bracedExpressionNode, true);
                    case 'X':
                        _position += 2;
                        BaseNode rangeBeginExpression = ParseExpression();
                        if (rangeBeginExpression == null)
                        {
                            return null;
                        }

                        BaseNode rangeEndExpression = ParseExpression();
                        if (rangeEndExpression == null)
                        {
                            return null;
                        }

                        bracedExpressionNode = ParseBracedExpression();
                        if (bracedExpressionNode == null)
                        {
                            return null;
                        }

                        return new BracedRangeExpression(rangeBeginExpression, rangeEndExpression, bracedExpressionNode);
                }
            }

            return ParseExpression();
        }

        //               ::= [gs] nw <expression>* _ <type> E                    # new (expr-list) type
        //               ::= [gs] nw <expression>* _ <type> <initializer>        # new (expr-list) type (init)
        //               ::= [gs] na <expression>* _ <type> E                    # new[] (expr-list) type
        //               ::= [gs] na <expression>* _ <type> <initializer>        # new[] (expr-list) type (init)
        //
        // <initializer> ::= pi <expression>* E                                  # parenthesized initialization
        private NewExpression ParseNewExpression()
        {
            bool isGlobal = ConsumeIf("gs");
            bool isArray = Peek(1) == 'a';

            if (!ConsumeIf("nw") || !ConsumeIf("na"))
            {
                return null;
            }

            List<BaseNode> expressions = new();
            List<BaseNode> initializers = new();

            while (!ConsumeIf("_"))
            {
                BaseNode expression = ParseExpression();
                if (expression == null)
                {
                    return null;
                }

                expressions.Add(expression);
            }

            BaseNode typeNode = ParseType();
            if (typeNode == null)
            {
                return null;
            }

            if (ConsumeIf("pi"))
            {
                while (!ConsumeIf("E"))
                {
                    BaseNode initializer = ParseExpression();
                    if (initializer == null)
                    {
                        return null;
                    }

                    initializers.Add(initializer);
                }
            }
            else if (!ConsumeIf("E"))
            {
                return null;
            }

            return new NewExpression(new NodeArray(expressions), typeNode, new NodeArray(initializers), isGlobal, isArray);
        }


        // <expression> ::= <unary operator-name> <expression>
        //              ::= <binary operator-name> <expression> <expression>
        //              ::= <ternary operator-name> <expression> <expression> <expression>
        //              ::= pp_ <expression>                                     # prefix ++
        //              ::= mm_ <expression>                                     # prefix --
        //              ::= cl <expression>+ E                                   # expression (expr-list), call
        //              ::= cv <type> <expression>                               # type (expression), conversion with one argument
        //              ::= cv <type> _ <expression>* E                          # type (expr-list), conversion with other than one argument
        //              ::= tl <type> <braced-expression>* E                     # type {expr-list}, conversion with braced-init-list argument
        //              ::= il <braced-expression>* E                            # {expr-list}, braced-init-list in any other context
        //              ::= [gs] nw <expression>* _ <type> E                     # new (expr-list) type
        //              ::= [gs] nw <expression>* _ <type> <initializer>         # new (expr-list) type (init)
        //              ::= [gs] na <expression>* _ <type> E                     # new[] (expr-list) type
        //              ::= [gs] na <expression>* _ <type> <initializer>         # new[] (expr-list) type (init)
        //              ::= [gs] dl <expression>                                 # delete expression
        //              ::= [gs] da <expression>                                 # delete[] expression
        //              ::= dc <type> <expression>                               # dynamic_cast<type> (expression)
        //              ::= sc <type> <expression>                               # static_cast<type> (expression)
        //              ::= cc <type> <expression>                               # const_cast<type> (expression)
        //              ::= rc <type> <expression>                               # reinterpret_cast<type> (expression)
        //              ::= ti <type>                                            # typeid (type)
        //              ::= te <expression>                                      # typeid (expression)
        //              ::= st <type>                                            # sizeof (type)
        //              ::= sz <expression>                                      # sizeof (expression)
        //              ::= at <type>                                            # alignof (type)
        //              ::= az <expression>                                      # alignof (expression)
        //              ::= nx <expression>                                      # noexcept (expression)
        //              ::= <template-param>
        //              ::= <function-param>
        //              ::= dt <expression> <unresolved-name>                    # expr.name
        //              ::= pt <expression> <unresolved-name>                    # expr->name
        //              ::= ds <expression> <expression>                         # expr.*expr
        //              ::= sZ <template-param>                                  # sizeof...(T), size of a template parameter pack
        //              ::= sZ <function-param>                                  # sizeof...(parameter), size of a function parameter pack
        //              ::= sP <template-arg>* E                                 # sizeof...(T), size of a captured template parameter pack from an alias template
        //              ::= sp <expression>                                      # expression..., pack expansion
        //              ::= tw <expression>                                      # throw expression
        //              ::= tr                                                   # throw with no operand (rethrow)
        //              ::= <unresolved-name>                                    # f(p), N::f(p), ::f(p),
        //                                                                       # freestanding dependent name (e.g., T::x),
        //                                                                       # objectless nonstatic member reference
        //              ::= <expr-primary>
        private BaseNode ParseExpression()
        {
            bool isGlobal = ConsumeIf("gs");
            BaseNode expression;
            if (Count() < 2)
            {
                return null;
            }

            switch (Peek())
            {
                case 'L':
                    return ParseExpressionPrimary();
                case 'T':
                    return ParseTemplateParam();
                case 'f':
                    char c = Peek(1);
                    if (c == 'p' || (c == 'L' && char.IsDigit(Peek(2))))
                    {
                        return ParseFunctionParameter();
                    }

                    return ParseFoldExpression();
                case 'a':
                    switch (Peek(1))
                    {
                        case 'a':
                            _position += 2;
                            return ParseBinaryExpression("&&");
                        case 'd':
                        case 'n':
                            _position += 2;
                            return ParseBinaryExpression("&");
                        case 'N':
                            _position += 2;
                            return ParseBinaryExpression("&=");
                        case 'S':
                            _position += 2;
                            return ParseBinaryExpression("=");
                        case 't':
                            _position += 2;
                            BaseNode type = ParseType();
                            if (type == null)
                            {
                                return null;
                            }

                            return new EnclosedExpression("alignof (", type, ")");
                        case 'z':
                            _position += 2;
                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new EnclosedExpression("alignof (", expression, ")");
                    }
                    return null;
                case 'c':
                    switch (Peek(1))
                    {
                        case 'c':
                            _position += 2;
                            BaseNode to = ParseType();
                            if (to == null)
                            {
                                return null;
                            }

                            BaseNode from = ParseExpression();
                            if (from == null)
                            {
                                return null;
                            }

                            return new CastExpression("const_cast", to, from);
                        case 'l':
                            _position += 2;
                            BaseNode callee = ParseExpression();
                            if (callee == null)
                            {
                                return null;
                            }

                            List<BaseNode> names = new();
                            while (!ConsumeIf("E"))
                            {
                                expression = ParseExpression();
                                if (expression == null)
                                {
                                    return null;
                                }

                                names.Add(expression);
                            }
                            return new CallExpression(callee, names);
                        case 'm':
                            _position += 2;
                            return ParseBinaryExpression(",");
                        case 'o':
                            _position += 2;
                            return ParsePrefixExpression("~");
                        case 'v':
                            return ParseConversionExpression();
                    }
                    return null;
                case 'd':
                    BaseNode leftNode;
                    BaseNode rightNode;
                    switch (Peek(1))
                    {
                        case 'a':
                            _position += 2;
                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return expression;
                            }

                            return new DeleteExpression(expression, isGlobal, true);
                        case 'c':
                            _position += 2;
                            BaseNode type = ParseType();
                            if (type == null)
                            {
                                return null;
                            }

                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return expression;
                            }

                            return new CastExpression("dynamic_cast", type, expression);
                        case 'e':
                            _position += 2;
                            return ParsePrefixExpression("*");
                        case 'l':
                            _position += 2;
                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new DeleteExpression(expression, isGlobal, false);
                        case 'n':
                            return ParseUnresolvedName();
                        case 's':
                            _position += 2;
                            leftNode = ParseExpression();
                            if (leftNode == null)
                            {
                                return null;
                            }

                            rightNode = ParseExpression();
                            if (rightNode == null)
                            {
                                return null;
                            }

                            return new MemberExpression(leftNode, ".*", rightNode);
                        case 't':
                            _position += 2;
                            leftNode = ParseExpression();
                            if (leftNode == null)
                            {
                                return null;
                            }

                            rightNode = ParseExpression();
                            if (rightNode == null)
                            {
                                return null;
                            }

                            return new MemberExpression(leftNode, ".", rightNode);
                        case 'v':
                            _position += 2;
                            return ParseBinaryExpression("/");
                        case 'V':
                            _position += 2;
                            return ParseBinaryExpression("/=");
                    }
                    return null;
                case 'e':
                    switch (Peek(1))
                    {
                        case 'o':
                            _position += 2;
                            return ParseBinaryExpression("^");
                        case 'O':
                            _position += 2;
                            return ParseBinaryExpression("^=");
                        case 'q':
                            _position += 2;
                            return ParseBinaryExpression("==");
                    }
                    return null;
                case 'g':
                    switch (Peek(1))
                    {
                        case 'e':
                            _position += 2;
                            return ParseBinaryExpression(">=");
                        case 't':
                            _position += 2;
                            return ParseBinaryExpression(">");
                    }
                    return null;
                case 'i':
                    switch (Peek(1))
                    {
                        case 'x':
                            _position += 2;
                            BaseNode Base = ParseExpression();
                            if (Base == null)
                            {
                                return null;
                            }

                            BaseNode subscript = ParseExpression();
                            if (Base == null)
                            {
                                return null;
                            }

                            return new ArraySubscriptingExpression(Base, subscript);
                        case 'l':
                            _position += 2;

                            List<BaseNode> bracedExpressions = new();
                            while (!ConsumeIf("E"))
                            {
                                expression = ParseBracedExpression();
                                if (expression == null)
                                {
                                    return null;
                                }

                                bracedExpressions.Add(expression);
                            }
                            return new InitListExpression(null, bracedExpressions);
                    }
                    return null;
                case 'l':
                    switch (Peek(1))
                    {
                        case 'e':
                            _position += 2;
                            return ParseBinaryExpression("<=");
                        case 's':
                            _position += 2;
                            return ParseBinaryExpression("<<");
                        case 'S':
                            _position += 2;
                            return ParseBinaryExpression("<<=");
                        case 't':
                            _position += 2;
                            return ParseBinaryExpression("<");
                    }
                    return null;
                case 'm':
                    switch (Peek(1))
                    {
                        case 'i':
                            _position += 2;
                            return ParseBinaryExpression("-");
                        case 'I':
                            _position += 2;
                            return ParseBinaryExpression("-=");
                        case 'l':
                            _position += 2;
                            return ParseBinaryExpression("*");
                        case 'L':
                            _position += 2;
                            return ParseBinaryExpression("*=");
                        case 'm':
                            _position += 2;
                            if (ConsumeIf("_"))
                            {
                                return ParsePrefixExpression("--");
                            }

                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new PostfixExpression(expression, "--");
                    }
                    return null;
                case 'n':
                    switch (Peek(1))
                    {
                        case 'a':
                        case 'w':
                            _position += 2;
                            return ParseNewExpression();
                        case 'e':
                            _position += 2;
                            return ParseBinaryExpression("!=");
                        case 'g':
                            _position += 2;
                            return ParsePrefixExpression("-");
                        case 't':
                            _position += 2;
                            return ParsePrefixExpression("!");
                        case 'x':
                            _position += 2;
                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new EnclosedExpression("noexcept (", expression, ")");
                    }
                    return null;
                case 'o':
                    switch (Peek(1))
                    {
                        case 'n':
                            return ParseUnresolvedName();
                        case 'o':
                            _position += 2;
                            return ParseBinaryExpression("||");
                        case 'r':
                            _position += 2;
                            return ParseBinaryExpression("|");
                        case 'R':
                            _position += 2;
                            return ParseBinaryExpression("|=");
                    }
                    return null;
                case 'p':
                    switch (Peek(1))
                    {
                        case 'm':
                            _position += 2;
                            return ParseBinaryExpression("->*");
                        case 'l':
                        case 's':
                            _position += 2;
                            return ParseBinaryExpression("+");
                        case 'L':
                            _position += 2;
                            return ParseBinaryExpression("+=");
                        case 'p':
                            _position += 2;
                            if (ConsumeIf("_"))
                            {
                                return ParsePrefixExpression("++");
                            }

                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new PostfixExpression(expression, "++");
                        case 't':
                            _position += 2;
                            leftNode = ParseExpression();
                            if (leftNode == null)
                            {
                                return null;
                            }

                            rightNode = ParseExpression();
                            if (rightNode == null)
                            {
                                return null;
                            }

                            return new MemberExpression(leftNode, "->", rightNode);
                    }
                    return null;
                case 'q':
                    if (Peek(1) == 'u')
                    {
                        _position += 2;
                        BaseNode condition = ParseExpression();
                        if (condition == null)
                        {
                            return null;
                        }

                        leftNode = ParseExpression();
                        if (leftNode == null)
                        {
                            return null;
                        }

                        rightNode = ParseExpression();
                        if (rightNode == null)
                        {
                            return null;
                        }

                        return new ConditionalExpression(condition, leftNode, rightNode);
                    }
                    return null;
                case 'r':
                    switch (Peek(1))
                    {
                        case 'c':
                            _position += 2;
                            BaseNode to = ParseType();
                            if (to == null)
                            {
                                return null;
                            }

                            BaseNode from = ParseExpression();
                            if (from == null)
                            {
                                return null;
                            }

                            return new CastExpression("reinterpret_cast", to, from);
                        case 'm':
                            _position += 2;
                            return ParseBinaryExpression("%");
                        case 'M':
                            _position += 2;
                            return ParseBinaryExpression("%");
                        case 's':
                            _position += 2;
                            return ParseBinaryExpression(">>");
                        case 'S':
                            _position += 2;
                            return ParseBinaryExpression(">>=");
                    }
                    return null;
                case 's':
                    switch (Peek(1))
                    {
                        case 'c':
                            _position += 2;
                            BaseNode to = ParseType();
                            if (to == null)
                            {
                                return null;
                            }

                            BaseNode from = ParseExpression();
                            if (from == null)
                            {
                                return null;
                            }

                            return new CastExpression("static_cast", to, from);
                        case 'p':
                            _position += 2;
                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new PackedTemplateParameterExpansion(expression);
                        case 'r':
                            return ParseUnresolvedName();
                        case 't':
                            _position += 2;
                            BaseNode enclosedType = ParseType();
                            if (enclosedType == null)
                            {
                                return null;
                            }

                            return new EnclosedExpression("sizeof (", enclosedType, ")");
                        case 'z':
                            _position += 2;
                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new EnclosedExpression("sizeof (", expression, ")");
                        case 'Z':
                            _position += 2;
                            BaseNode sizeofParamNode;
                            switch (Peek())
                            {
                                case 'T':
                                    // FIXME: ??? Not entire sure if it's right
                                    sizeofParamNode = ParseFunctionParameter();
                                    if (sizeofParamNode == null)
                                    {
                                        return null;
                                    }

                                    return new EnclosedExpression("sizeof...(", new PackedTemplateParameterExpansion(sizeofParamNode), ")");
                                case 'f':
                                    sizeofParamNode = ParseFunctionParameter();
                                    if (sizeofParamNode == null)
                                    {
                                        return null;
                                    }

                                    return new EnclosedExpression("sizeof...(", sizeofParamNode, ")");
                            }
                            return null;
                        case 'P':
                            _position += 2;
                            List<BaseNode> arguments = new();
                            while (!ConsumeIf("E"))
                            {
                                BaseNode argument = ParseTemplateArgument();
                                if (argument == null)
                                {
                                    return null;
                                }

                                arguments.Add(argument);
                            }
                            return new EnclosedExpression("sizeof...(", new NodeArray(arguments), ")");
                    }
                    return null;
                case 't':
                    switch (Peek(1))
                    {
                        case 'e':
                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new EnclosedExpression("typeid (", expression, ")");
                        case 't':
                            BaseNode enclosedType = ParseExpression();
                            if (enclosedType == null)
                            {
                                return null;
                            }

                            return new EnclosedExpression("typeid (", enclosedType, ")");
                        case 'l':
                            _position += 2;
                            BaseNode typeNode = ParseType();
                            if (typeNode == null)
                            {
                                return null;
                            }

                            List<BaseNode> bracedExpressions = new();
                            while (!ConsumeIf("E"))
                            {
                                expression = ParseBracedExpression();
                                if (expression == null)
                                {
                                    return null;
                                }

                                bracedExpressions.Add(expression);
                            }
                            return new InitListExpression(typeNode, bracedExpressions);
                        case 'r':
                            _position += 2;
                            return new NameType("throw");
                        case 'w':
                            _position += 2;
                            expression = ParseExpression();
                            if (expression == null)
                            {
                                return null;
                            }

                            return new ThrowExpression(expression);
                    }
                    return null;
            }

            if (char.IsDigit(Peek()))
            {
                return ParseUnresolvedName();
            }

            return null;
        }

        private IntegerLiteral ParseIntegerLiteral(string literalName)
        {
            string number = ParseNumber(true);
            if (number == null || number.Length == 0 || !ConsumeIf("E"))
            {
                return null;
            }

            return new IntegerLiteral(literalName, number);
        }

        // <expr-primary> ::= L <type> <value number> E                          # integer literal
        //                ::= L <type> <value float> E                           # floating literal (TODO)
        //                ::= L <string type> E                                  # string literal
        //                ::= L <nullptr type> E                                 # nullptr literal (i.e., "LDnE")
        //                ::= L <pointer type> 0 E                               # null pointer template argument
        //                ::= L <type> <real-part float> _ <imag-part float> E   # complex floating point literal (C 2000)
        //                ::= L _Z <encoding> E                                  # external name
        private BaseNode ParseExpressionPrimary()
        {
            if (!ConsumeIf("L"))
            {
                return null;
            }

            switch (Peek())
            {
                case 'w':
                    _position++;
                    return ParseIntegerLiteral("wchar_t");
                case 'b':
                    if (ConsumeIf("b0E"))
                    {
                        return new NameType("false", NodeType.BooleanExpression);
                    }

                    if (ConsumeIf("b1E"))
                    {
                        return new NameType("true", NodeType.BooleanExpression);
                    }

                    return null;
                case 'c':
                    _position++;
                    return ParseIntegerLiteral("char");
                case 'a':
                    _position++;
                    return ParseIntegerLiteral("signed char");
                case 'h':
                    _position++;
                    return ParseIntegerLiteral("unsigned char");
                case 's':
                    _position++;
                    return ParseIntegerLiteral("short");
                case 't':
                    _position++;
                    return ParseIntegerLiteral("unsigned short");
                case 'i':
                    _position++;
                    return ParseIntegerLiteral("");
                case 'j':
                    _position++;
                    return ParseIntegerLiteral("u");
                case 'l':
                    _position++;
                    return ParseIntegerLiteral("l");
                case 'm':
                    _position++;
                    return ParseIntegerLiteral("ul");
                case 'x':
                    _position++;
                    return ParseIntegerLiteral("ll");
                case 'y':
                    _position++;
                    return ParseIntegerLiteral("ull");
                case 'n':
                    _position++;
                    return ParseIntegerLiteral("__int128");
                case 'o':
                    _position++;
                    return ParseIntegerLiteral("unsigned __int128");
                case 'd':
                case 'e':
                case 'f':
                    // TODO: floating literal
                    return null;
                case '_':
                    if (ConsumeIf("_Z"))
                    {
                        BaseNode encoding = ParseEncoding();
                        if (encoding != null && ConsumeIf("E"))
                        {
                            return encoding;
                        }
                    }
                    return null;
                case 'T':
                    return null;
                default:
                    BaseNode type = ParseType();
                    if (type == null)
                    {
                        return null;
                    }

                    string number = ParseNumber();
                    if (number == null || number.Length == 0 || !ConsumeIf("E"))
                    {
                        return null;
                    }

                    return new IntegerCastExpression(type, number);
            }
        }

        // <decltype>  ::= Dt <expression> E  # decltype of an id-expression or class member access (C++0x)
        //             ::= DT <expression> E  # decltype of an expression (C++0x)
        private EnclosedExpression ParseDecltype()
        {
            if (!ConsumeIf("D") || (!ConsumeIf("t") && !ConsumeIf("T")))
            {
                return null;
            }

            BaseNode expression = ParseExpression();
            if (expression == null)
            {
                return null;
            }

            if (!ConsumeIf("E"))
            {
                return null;
            }

            return new EnclosedExpression("decltype(", expression, ")");
        }

        // <template-param>          ::= T_ # first template parameter
        //                           ::= T <parameter-2 non-negative number> _
        // <template-template-param> ::= <template-param>
        //                           ::= <substitution>
        private BaseNode ParseTemplateParam()
        {
            if (!ConsumeIf("T"))
            {
                return null;
            }

            int index = 0;
            if (!ConsumeIf("_"))
            {
                index = ParsePositiveNumber();
                if (index < 0)
                {
                    return null;
                }

                index++;
                if (!ConsumeIf("_"))
                {
                    return null;
                }
            }

            // 5.1.8: TODO: lambda?
            // if (IsParsingLambdaParameters)
            //    return new NameType("auto");

            if (_canForwardTemplateReference)
            {
                ForwardTemplateReference forwardTemplateReference = new(index);
                _forwardTemplateReferenceList.Add(forwardTemplateReference);
                return forwardTemplateReference;
            }
            if (index >= _templateParamList.Count)
            {
                return null;
            }

            return _templateParamList[index];
        }

        // <template-args> ::= I <template-arg>+ E
        private TemplateArguments ParseTemplateArguments(bool hasContext = false)
        {
            if (!ConsumeIf("I"))
            {
                return null;
            }

            if (hasContext)
            {
                _templateParamList.Clear();
            }

            List<BaseNode> args = new();
            while (!ConsumeIf("E"))
            {
                if (hasContext)
                {
                    List<BaseNode> templateParamListTemp = new(_templateParamList);
                    BaseNode templateArgument = ParseTemplateArgument();
                    _templateParamList = templateParamListTemp;
                    if (templateArgument == null)
                    {
                        return null;
                    }

                    args.Add(templateArgument);
                    if (templateArgument.GetType().Equals(NodeType.PackedTemplateArgument))
                    {
                        templateArgument = new PackedTemplateParameter(((NodeArray)templateArgument).Nodes);
                    }
                    _templateParamList.Add(templateArgument);
                }
                else
                {
                    BaseNode templateArgument = ParseTemplateArgument();
                    if (templateArgument == null)
                    {
                        return null;
                    }

                    args.Add(templateArgument);
                }
            }
            return new TemplateArguments(args);
        }


        // <template-arg> ::= <type>                                             # type or template
        //                ::= X <expression> E                                   # expression
        //                ::= <expr-primary>                                     # simple expressions
        //                ::= J <template-arg>* E                                # argument pack
        private BaseNode ParseTemplateArgument()
        {
            switch (Peek())
            {
                // X <expression> E
                case 'X':
                    _position++;
                    BaseNode expression = ParseExpression();
                    if (expression == null || !ConsumeIf("E"))
                    {
                        return null;
                    }

                    return expression;
                // <expr-primary>
                case 'L':
                    return ParseExpressionPrimary();
                // J <template-arg>* E
                case 'J':
                    _position++;
                    List<BaseNode> templateArguments = new();
                    while (!ConsumeIf("E"))
                    {
                        BaseNode templateArgument = ParseTemplateArgument();
                        if (templateArgument == null)
                        {
                            return null;
                        }

                        templateArguments.Add(templateArgument);
                    }
                    return new NodeArray(templateArguments, NodeType.PackedTemplateArgument);
                // <type>
                default:
                    return ParseType();
            }
        }

        class NameParserContext
        {
            public CvType Cv;
            public SimpleReferenceType Ref;
            public bool FinishWithTemplateArguments;
            public bool CtorDtorConversion;
        }


        //   <unresolved-type> ::= <template-param> [ <template-args> ]            # T:: or T<X,Y>::
        //                     ::= <decltype>                                      # decltype(p)::
        //                     ::= <substitution>
        private BaseNode ParseUnresolvedType()
        {
            if (Peek() == 'T')
            {
                BaseNode templateParam = ParseTemplateParam();
                if (templateParam == null)
                {
                    return null;
                }

                _substitutionList.Add(templateParam);
                return templateParam;
            }
            else if (Peek() == 'D')
            {
                BaseNode declType = ParseDecltype();
                if (declType == null)
                {
                    return null;
                }

                _substitutionList.Add(declType);
                return declType;
            }
            return ParseSubstitution();
        }

        // <simple-id> ::= <source-name> [ <template-args> ]
        private BaseNode ParseSimpleId()
        {
            BaseNode sourceName = ParseSourceName();
            if (sourceName == null)
            {
                return null;
            }

            if (Peek() == 'I')
            {
                BaseNode templateArguments = ParseTemplateArguments();
                if (templateArguments == null)
                {
                    return null;
                }

                return new NameTypeWithTemplateArguments(sourceName, templateArguments);
            }
            return sourceName;
        }

        //  <destructor-name> ::= <unresolved-type>                               # e.g., ~T or ~decltype(f())
        //                    ::= <simple-id>                                     # e.g., ~A<2*N>
        private DtorName ParseDestructorName()
        {
            BaseNode node;
            if (char.IsDigit(Peek()))
            {
                node = ParseSimpleId();
            }
            else
            {
                node = ParseUnresolvedType();
            }
            if (node == null)
            {
                return null;
            }

            return new DtorName(node);
        }

        //  <base-unresolved-name> ::= <simple-id>                                # unresolved name
        //  extension              ::= <operator-name>                            # unresolved operator-function-id
        //  extension              ::= <operator-name> <template-args>            # unresolved operator template-id
        //                         ::= on <operator-name>                         # unresolved operator-function-id
        //                         ::= on <operator-name> <template-args>         # unresolved operator template-id
        //                         ::= dn <destructor-name>                       # destructor or pseudo-destructor;
        //                                                                        # e.g. ~X or ~X<N-1>
        private BaseNode ParseBaseUnresolvedName()
        {
            if (char.IsDigit(Peek()))
            {
                return ParseSimpleId();
            }
            else if (ConsumeIf("dn"))
            {
                return ParseDestructorName();
            }

            ConsumeIf("on");
            BaseNode operatorName = ParseOperatorName(null);
            if (operatorName == null)
            {
                return null;
            }

            if (Peek() == 'I')
            {
                BaseNode templateArguments = ParseTemplateArguments();
                if (templateArguments == null)
                {
                    return null;
                }

                return new NameTypeWithTemplateArguments(operatorName, templateArguments);
            }
            return operatorName;
        }

        // <unresolved-name> ::= [gs] <base-unresolved-name>                     # x or (with "gs") ::x
        //                   ::= sr <unresolved-type> <base-unresolved-name>     # T::x / decltype(p)::x
        //                   ::= srN <unresolved-type> <unresolved-qualifier-level>+ E <base-unresolved-name>
        //                                                                       # T::N::x /decltype(p)::N::x
        //                   ::= [gs] sr <unresolved-qualifier-level>+ E <base-unresolved-name>
        //                                                                       # A::x, N::y, A<T>::z; "gs" means leading "::"
        private BaseNode ParseUnresolvedName(NameParserContext context = null)
        {
            BaseNode result = null;
            if (ConsumeIf("srN"))
            {
                result = ParseUnresolvedType();
                if (result == null)
                {
                    return null;
                }

                if (Peek() == 'I')
                {
                    BaseNode templateArguments = ParseTemplateArguments();
                    if (templateArguments == null)
                    {
                        return null;
                    }

                    result = new NameTypeWithTemplateArguments(result, templateArguments);
                    if (result == null)
                    {
                        return null;
                    }
                }

                while (!ConsumeIf("E"))
                {
                    BaseNode simpleId = ParseSimpleId();
                    if (simpleId == null)
                    {
                        return null;
                    }

                    result = new QualifiedName(result, simpleId);
                    if (result == null)
                    {
                        return null;
                    }
                }

                BaseNode baseName = ParseBaseUnresolvedName();
                if (baseName == null)
                {
                    return null;
                }

                return new QualifiedName(result, baseName);
            }

            bool isGlobal = ConsumeIf("gs");

            // ::= [gs] <base-unresolved-name>                     # x or (with "gs") ::x
            if (!ConsumeIf("sr"))
            {
                result = ParseBaseUnresolvedName();
                if (result == null)
                {
                    return null;
                }

                if (isGlobal)
                {
                    result = new GlobalQualifiedName(result);
                }

                return result;
            }

            // ::= [gs] sr <unresolved-qualifier-level>+ E <base-unresolved-name>
            if (char.IsDigit(Peek()))
            {
                do
                {
                    BaseNode qualifier = ParseSimpleId();
                    if (qualifier == null)
                    {
                        return null;
                    }

                    if (result != null)
                    {
                        result = new QualifiedName(result, qualifier);
                    }
                    else if (isGlobal)
                    {
                        result = new GlobalQualifiedName(qualifier);
                    }
                    else
                    {
                        result = qualifier;
                    }

                    if (result == null)
                    {
                        return null;
                    }
                } while (!ConsumeIf("E"));
            }
            // ::= sr <unresolved-type> [template-args] <base-unresolved-name>     # T::x / decltype(p)::x
            else
            {
                result = ParseUnresolvedType();
                if (result == null)
                {
                    return null;
                }

                if (Peek() == 'I')
                {
                    BaseNode templateArguments = ParseTemplateArguments();
                    if (templateArguments == null)
                    {
                        return null;
                    }

                    result = new NameTypeWithTemplateArguments(result, templateArguments);
                    if (result == null)
                    {
                        return null;
                    }
                }
            }

            if (result == null)
            {
                return null;
            }

            BaseNode baseUnresolvedName = ParseBaseUnresolvedName();
            if (baseUnresolvedName == null)
            {
                return null;
            }

            return new QualifiedName(result, baseUnresolvedName);
        }

        //    <unscoped-name> ::= <unqualified-name>
        //                    ::= St <unqualified-name>   # ::std::
        private BaseNode ParseUnscopedName(NameParserContext context)
        {
            if (ConsumeIf("St"))
            {
                BaseNode unresolvedName = ParseUnresolvedName(context);
                if (unresolvedName == null)
                {
                    return null;
                }

                return new StdQualifiedName(unresolvedName);
            }
            return ParseUnresolvedName(context);
        }

        // <nested-name> ::= N [<CV-qualifiers>] [<ref-qualifier>] <prefix (TODO)> <unqualified-name> E
        //               ::= N [<CV-qualifiers>] [<ref-qualifier>] <template-prefix (TODO)> <template-args (TODO)> E
        private BaseNode ParseNestedName(NameParserContext context)
        {
            // Impossible in theory
            if (Consume() != 'N')
            {
                return null;
            }

            BaseNode result = null;
            CvType cv = new(ParseCvQualifiers(), null);
            if (context != null)
            {
                context.Cv = cv;
            }

            SimpleReferenceType Ref = ParseRefQualifiers();
            if (context != null)
            {
                context.Ref = Ref;
            }

            if (ConsumeIf("St"))
            {
                result = new NameType("std");
            }

            while (!ConsumeIf("E"))
            {
                // <data-member-prefix> end
                if (ConsumeIf("M"))
                {
                    if (result == null)
                    {
                        return null;
                    }

                    continue;
                }
                char c = Peek();

                // TODO: template args
                if (c == 'T')
                {
                    BaseNode templateParam = ParseTemplateParam();
                    if (templateParam == null)
                    {
                        return null;
                    }

                    result = CreateNameNode(result, templateParam, context);
                    _substitutionList.Add(result);
                    continue;
                }

                // <template-prefix> <template-args>
                if (c == 'I')
                {
                    BaseNode templateArgument = ParseTemplateArguments(context != null);
                    if (templateArgument == null || result == null)
                    {
                        return null;
                    }

                    result = new NameTypeWithTemplateArguments(result, templateArgument);
                    if (context != null)
                    {
                        context.FinishWithTemplateArguments = true;
                    }

                    _substitutionList.Add(result);
                    continue;
                }

                // <decltype>
                if (c == 'D' && (Peek(1) == 't' || Peek(1) == 'T'))
                {
                    BaseNode decltype = ParseDecltype();
                    if (decltype == null)
                    {
                        return null;
                    }

                    result = CreateNameNode(result, decltype, context);
                    _substitutionList.Add(result);
                    continue;
                }

                // <substitution>
                if (c == 'S' && Peek(1) != 't')
                {
                    BaseNode substitution = ParseSubstitution();
                    if (substitution == null)
                    {
                        return null;
                    }

                    result = CreateNameNode(result, substitution, context);
                    if (result != substitution)
                    {
                        _substitutionList.Add(substitution);
                    }

                    continue;
                }

                // <ctor-dtor-name> of ParseUnqualifiedName
                if (c == 'C' || (c == 'D' && Peek(1) != 'C'))
                {
                    // We cannot have nothing before this
                    if (result == null)
                    {
                        return null;
                    }

                    BaseNode ctOrDtorName = ParseCtorDtorName(context, result);

                    if (ctOrDtorName == null)
                    {
                        return null;
                    }

                    result = CreateNameNode(result, ctOrDtorName, context);

                    // TODO: ABI Tags (before)
                    if (result == null)
                    {
                        return null;
                    }

                    _substitutionList.Add(result);
                    continue;
                }

                BaseNode unqualifiedName = ParseUnqualifiedName(context);
                if (unqualifiedName == null)
                {
                    return null;
                }
                result = CreateNameNode(result, unqualifiedName, context);

                _substitutionList.Add(result);
            }
            if (result == null || _substitutionList.Count == 0)
            {
                return null;
            }

            _substitutionList.RemoveAt(_substitutionList.Count - 1);
            return result;
        }

        //   <discriminator> ::= _ <non-negative number>      # when number < 10
        //                   ::= __ <non-negative number> _   # when number >= 10
        private void ParseDiscriminator()
        {
            if (Count() == 0)
            {
                return;
            }
            // We ignore the discriminator, we don't need it.
            if (ConsumeIf("_"))
            {
                ConsumeIf("_");
                while (char.IsDigit(Peek()) && Count() != 0)
                {
                    Consume();
                }
                ConsumeIf("_");
            }
        }

        //   <local-name> ::= Z <function encoding> E <entity name> [<discriminator>]
        //                ::= Z <function encoding> E s [<discriminator>]
        //                ::= Z <function encoding> Ed [ <parameter number> ] _ <entity name>
        private LocalName ParseLocalName(NameParserContext context)
        {
            if (!ConsumeIf("Z"))
            {
                return null;
            }

            BaseNode encoding = ParseEncoding();
            if (encoding == null || !ConsumeIf("E"))
            {
                return null;
            }

            BaseNode entityName;
            if (ConsumeIf("s"))
            {
                ParseDiscriminator();
                return new LocalName(encoding, new NameType("string literal"));
            }
            else if (ConsumeIf("d"))
            {
                ParseNumber(true);
                if (!ConsumeIf("_"))
                {
                    return null;
                }

                entityName = ParseName(context);
                if (entityName == null)
                {
                    return null;
                }

                return new LocalName(encoding, entityName);
            }

            entityName = ParseName(context);
            if (entityName == null)
            {
                return null;
            }

            ParseDiscriminator();
            return new LocalName(encoding, entityName);
        }

        // <name> ::= <nested-name>
        //        ::= <unscoped-name>
        //        ::= <unscoped-template-name> <template-args>
        //        ::= <local-name>  # See Scope Encoding below (TODO)
        private BaseNode ParseName(NameParserContext context = null)
        {
            ConsumeIf("L");

            if (Peek() == 'N')
            {
                return ParseNestedName(context);
            }

            if (Peek() == 'Z')
            {
                return ParseLocalName(context);
            }

            if (Peek() == 'S' && Peek(1) != 't')
            {
                BaseNode substitution = ParseSubstitution();
                if (substitution == null)
                {
                    return null;
                }

                if (Peek() != 'I')
                {
                    return null;
                }

                BaseNode templateArguments = ParseTemplateArguments(context != null);
                if (templateArguments == null)
                {
                    return null;
                }

                if (context != null)
                {
                    context.FinishWithTemplateArguments = true;
                }

                return new NameTypeWithTemplateArguments(substitution, templateArguments);
            }

            BaseNode result = ParseUnscopedName(context);
            if (result == null)
            {
                return null;
            }

            if (Peek() == 'I')
            {
                _substitutionList.Add(result);
                BaseNode templateArguments = ParseTemplateArguments(context != null);
                if (templateArguments == null)
                {
                    return null;
                }

                if (context != null)
                {
                    context.FinishWithTemplateArguments = true;
                }

                return new NameTypeWithTemplateArguments(result, templateArguments);
            }

            return result;
        }

        private bool IsEncodingEnd()
        {
            char c = Peek();
            return Count() == 0 || c == 'E' || c == '.' || c == '_';
        }

        // <encoding> ::= <function name> <bare-function-type>
        //            ::= <data name>
        //            ::= <special-name>
        private BaseNode ParseEncoding()
        {
            NameParserContext context = new();
            if (Peek() == 'T' || (Peek() == 'G' && Peek(1) == 'V'))
            {
                return ParseSpecialName(context);
            }

            BaseNode name = ParseName(context);
            if (name == null)
            {
                return null;
            }

            // TODO: compute template refs here

            if (IsEncodingEnd())
            {
                return name;
            }

            // TODO: Ua9enable_ifI

            BaseNode returnType = null;
            if (!context.CtorDtorConversion && context.FinishWithTemplateArguments)
            {
                returnType = ParseType();
                if (returnType == null)
                {
                    return null;
                }
            }

            if (ConsumeIf("v"))
            {
                return new EncodedFunction(name, null, context.Cv, context.Ref, null, returnType);
            }

            List<BaseNode> paramsList = new();

            // backup because that can be destroyed by parseType
            CvType cv = context.Cv;
            SimpleReferenceType Ref = context.Ref;

            while (!IsEncodingEnd())
            {
                BaseNode param = ParseType();
                if (param == null)
                {
                    return null;
                }

                paramsList.Add(param);
            }

            return new EncodedFunction(name, new NodeArray(paramsList), cv, Ref, null, returnType);
        }

        // <mangled-name> ::= _Z <encoding>
        //                ::= <type>
        private BaseNode Parse()
        {
            if (ConsumeIf("_Z"))
            {
                BaseNode encoding = ParseEncoding();
                if (encoding != null && Count() == 0)
                {
                    return encoding;
                }
                return null;
            }
            else
            {
                BaseNode type = ParseType();
                if (type != null && Count() == 0)
                {
                    return type;
                }
                return null;
            }
        }

        public static string Parse(string originalMangled)
        {
            Demangler instance = new(originalMangled);
            BaseNode resNode = instance.Parse();

            if (resNode != null)
            {
                StringWriter writer = new();
                resNode.Print(writer);
                return writer.ToString();
            }

            return originalMangled;
        }
    }
}
