// Утилита: выводит из .NET-сборки ссылки на сборки, вызываемые внешние методы
// и все строковые литералы (#US heap).
// Режим ldstr: для каждого строкового литерала — метод, где он встречается,
// и ближайший следующий вызов (куда строка уходит). Формат: метод \t вызов \t строка.
// Режим calls: все вызовы каждого метода (метод \t вызов) — видно, пишет ли метод в журнал или на экран.
// Использование: dotnet run -- <путь к dll> [refs|members|strings|ldstr|calls|all]
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var path = args[0];
var mode = args.Length > 1 ? args[1] : "all";

using var fs = File.OpenRead(path);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();

if (mode is "refs" or "all")
{
    Console.WriteLine("==== assembly refs");
    foreach (var h in md.AssemblyReferences)
        Console.WriteLine(md.GetString(md.GetAssemblyReference(h).Name));
}

if (mode is "members" or "all")
{
    Console.WriteLine("==== member refs");
    var seen = new SortedSet<string>();
    foreach (var h in md.MemberReferences)
    {
        var mr = md.GetMemberReference(h);
        string owner = mr.Parent.Kind switch
        {
            HandleKind.TypeReference => TypeNameOf(md.GetTypeReference((TypeReferenceHandle)mr.Parent)),
            _ => "?"
        };
        seen.Add(owner + "::" + md.GetString(mr.Name));
    }
    foreach (var s in seen) Console.WriteLine(s);
}

if (mode is "strings" or "all")
{
    Console.WriteLine("==== user strings");
    var h = MetadataTokens.UserStringHandle(1);
    while (!h.IsNil)
    {
        var s = md.GetUserString(h);
        Console.WriteLine(Escape(s));
        h = md.GetNextHandle(h);
    }
}

if (mode is "ldstr" or "calls")
{
    var oneByte = new OpCode[0x100];
    var twoByte = new OpCode[0x100];
    foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
    {
        var op = (OpCode)f.GetValue(null);
        var v = (ushort)op.Value;
        if (v < 0x100) oneByte[v] = op;
        else if ((v & 0xFF00) == 0xFE00) twoByte[v & 0xFF] = op;
    }

    foreach (var th in md.TypeDefinitions)
    {
        var td = md.GetTypeDefinition(th);
        var typeName = DefName(td);
        foreach (var mh in td.GetMethods())
        {
            var mdef = md.GetMethodDefinition(mh);
            if (mdef.RelativeVirtualAddress == 0) continue;
            var il = pe.GetMethodBody(mdef.RelativeVirtualAddress).GetILBytes();
            var where = typeName + "::" + md.GetString(mdef.Name);

            // Первый проход: декодируем инструкции.
            var ins = new List<(OpCode op, int token)>();
            int pos = 0;
            while (pos < il.Length)
            {
                OpCode op;
                byte b = il[pos++];
                if (b == 0xFE) op = twoByte[il[pos++]];
                else op = oneByte[b];
                int token = 0;
                switch (op.OperandType)
                {
                    case OperandType.InlineNone: break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: pos += 1; break;
                    case OperandType.InlineVar: pos += 2; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: pos += 8; break;
                    case OperandType.InlineSwitch:
                        int n = BitConverter.ToInt32(il, pos);
                        pos += 4 + n * 4; break;
                    default:
                        token = BitConverter.ToInt32(il, pos);
                        pos += 4; break;
                }
                ins.Add((op, token));
            }

            if (mode == "calls")
            {
                foreach (var (op, token) in ins)
                    if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj)
                        Console.WriteLine(where + "\t" + MethodName(token));
                continue;
            }

            for (int i = 0; i < ins.Count; i++)
            {
                if (ins[i].op != OpCodes.Ldstr) continue;
                var s = md.GetUserString(MetadataTokens.UserStringHandle(ins[i].token & 0xFFFFFF));
                string next = "-";
                for (int k = i + 1; k < ins.Count && k <= i + 8; k++)
                {
                    var o = ins[k].op;
                    if (o == OpCodes.Call || o == OpCodes.Callvirt || o == OpCodes.Newobj)
                    {
                        // Вызов без параметров строку не потребляет (кроме "строка".Метод() сразу за ldstr).
                        var (count, instance) = Signature(ins[k].token);
                        if (count == 0 && o != OpCodes.Newobj && (!instance || k != i + 1)) continue;
                        next = MethodName(ins[k].token);
                        break;
                    }
                    if (o == OpCodes.Stfld || o == OpCodes.Stsfld || o == OpCodes.Ret) { next = o.Name; break; }
                }
                Console.WriteLine(where + "\t" + next + "\t" + Escape(s));
            }
        }
    }
}

string TypeNameOf(TypeReference t) => md.GetString(t.Namespace) + "." + md.GetString(t.Name);

string DefName(TypeDefinition td)
{
    var name = md.GetString(td.Name);
    var decl = td.GetDeclaringType();
    return decl.IsNil ? name : DefName(md.GetTypeDefinition(decl)) + "/" + name;
}

(int count, bool instance) Signature(int token)
{
    var h = MetadataTokens.EntityHandle(token);
    BlobHandle blob;
    switch (h.Kind)
    {
        case HandleKind.MethodDefinition: blob = md.GetMethodDefinition((MethodDefinitionHandle)h).Signature; break;
        case HandleKind.MemberReference: blob = md.GetMemberReference((MemberReferenceHandle)h).Signature; break;
        case HandleKind.MethodSpecification:
            return Signature(MetadataTokens.GetToken(md.GetMethodSpecification((MethodSpecificationHandle)h).Method));
        default: return (-1, false);
    }
    var reader = md.GetBlobReader(blob);
    var header = reader.ReadSignatureHeader();
    if (header.IsGeneric) reader.ReadCompressedInteger();
    return (reader.ReadCompressedInteger(), header.IsInstance);
}

string MethodName(int token)
{
    var h = MetadataTokens.EntityHandle(token);
    switch (h.Kind)
    {
        case HandleKind.MethodDefinition:
        {
            var m = md.GetMethodDefinition((MethodDefinitionHandle)h);
            return DefName(md.GetTypeDefinition(m.GetDeclaringType())) + "::" + md.GetString(m.Name);
        }
        case HandleKind.MemberReference:
        {
            var mr = md.GetMemberReference((MemberReferenceHandle)h);
            string owner = mr.Parent.Kind switch
            {
                HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)mr.Parent).Name),
                HandleKind.TypeDefinition => DefName(md.GetTypeDefinition((TypeDefinitionHandle)mr.Parent)),
                HandleKind.TypeSpecification => "generic",
                _ => "?"
            };
            return owner + "::" + md.GetString(mr.Name);
        }
        case HandleKind.MethodSpecification:
            return MethodName(MetadataTokens.GetToken(md.GetMethodSpecification((MethodSpecificationHandle)h).Method));
        default:
            return "?";
    }
}

static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
