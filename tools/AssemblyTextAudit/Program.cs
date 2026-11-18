using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// Reads shipped CIL without loading or executing game assemblies. Output is private
// audit data; never package it or the game binaries with the mod.
if (args.Length < 2) throw new ArgumentException("Usage: AssemblyTextAudit OUTPUT.json ASSEMBLY.dll [...]");
var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
    .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null)!)
    .ToDictionary(o => unchecked((ushort)o.Value));
var assemblies = new List<object>();
foreach (string path in args.Skip(1))
{
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var md = pe.GetMetadataReader();
    var rows = new List<object>();
    int methods = 0;
    string TypeName(TypeDefinitionHandle h)
    {
        var t = md.GetTypeDefinition(h);
        return t.IsNested ? TypeName(t.GetDeclaringType()) + "+" + md.GetString(t.Name)
            : md.GetString(t.Namespace) + "." + md.GetString(t.Name);
    }
    foreach (var th in md.TypeDefinitions)
    {
        var type = md.GetTypeDefinition(th);
        foreach (var mh in type.GetMethods())
        {
            var method = md.GetMethodDefinition(mh);
            if (method.RelativeVirtualAddress == 0) continue;
            methods++;
            var bytes = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!;
            for (int i = 0; i < bytes.Length;)
            {
                int offset = i;
                ushort code = bytes[i++];
                if (code == 0xfe) code = (ushort)(0xfe00 | bytes[i++]);
                if (!opcodes.TryGetValue(code, out var op)) throw new InvalidDataException($"Unknown CIL opcode at {offset}");
                int size = op.OperandType switch
                {
                    OperandType.InlineNone => 0,
                    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                    OperandType.InlineVar => 2,
                    OperandType.InlineI8 or OperandType.InlineR => 8,
                    OperandType.InlineSwitch => 4 + checked(4 * BitConverter.ToInt32(bytes, i)),
                    _ => 4
                };
                if (op == OpCodes.Ldstr)
                {
                    int token = BitConverter.ToInt32(bytes, i);
                    rows.Add(new { type = TypeName(th), method = md.GetString(method.Name),
                        methodToken = MetadataTokens.GetToken(mh), ilOffset = offset,
                        text = md.GetUserString(MetadataTokens.UserStringHandle(token & 0xffffff)) });
                }
                i += size;
                if (i > bytes.Length) throw new InvalidDataException("Truncated CIL operand");
            }
        }
        foreach (var fh in type.GetFields())
        {
            var f = md.GetFieldDefinition(fh);
            var ch = f.GetDefaultValue();
            if (ch.IsNil) continue;
            var constant = md.GetConstant(ch);
            if (constant.TypeCode != ConstantTypeCode.String) continue;
            rows.Add(new { type = TypeName(th), field = md.GetString(f.Name),
                text = System.Text.Encoding.Unicode.GetString(md.GetBlobBytes(constant.Value)) });
        }
    }
    assemblies.Add(new { name = Path.GetFileName(path), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
        mvid = md.GetGuid(md.GetModuleDefinition().Mvid), methods, rows });
}
File.WriteAllText(args[0], JsonSerializer.Serialize(assemblies, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"CIL string inventory written for {assemblies.Count} assemblies.");
