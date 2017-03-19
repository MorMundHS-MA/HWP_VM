namespace HWP_VM
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;


    public static class Assembler
    {
        public static byte[] Assemble(string source)
        {
            var lines = source.Split('\r', '\n');
            return AssembleBlock(lines);
        }

        public static byte[] AssembleBlock(IEnumerable<string> asmOnlySource)
        {
            List<byte> asm = new List<byte>();
            foreach (var line in asmOnlySource)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var op = new Op(line);
                byte b1, b2;
                op.ToBinaryOp(out b1, out b2);
                asm.Add(b1);
                asm.Add(b2);
            }

            return asm.ToArray();
        }

        public static string DisassembleBlock(byte[] binary)
        {
            StringBuilder disasm = new StringBuilder();
            for (int i = 0; i < binary.Length; i += 2)
            {
                var op = new Op((ushort)(binary[i] << 8 | binary[i + 1]));
                disasm.Append(op.ToString());
            }

            return disasm.ToString();
        }

        private static string RemoveComments(string line)
        {
            var commentStart = line.IndexOf(';');
            if (commentStart == -1)
            {
                return line;
            }

            return line.Substring(0, commentStart);
        }

    }
}
