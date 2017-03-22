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
            var labels = new Dictionary<string, Label>();
            (string name, int pos, int line) currentLabel = (string.Empty, 0, 0);
            var currentAsmSize = 0;
            var currentBinPos = 0;

            // Collect label names
            for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
            {
                var tokens = RemoveComments(lines[lineNumber])
                    .Split()
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToArray();

                if (tokens.Length == 0 || string.IsNullOrWhiteSpace(tokens[0]))
                {
                    continue;
                }

                switch (tokens[0])
                {
                    case "LABEL":
                        if (tokens.Length != 2 || string.IsNullOrWhiteSpace(tokens[1]))
                        {
                            throw new AssemblerException("Empty label name!", null, lineNumber + 1, lines[lineNumber]);
                        }

                        CompleteCurrentLabel(labels, currentLabel, lineNumber - currentLabel.line, ref currentAsmSize, ref currentBinPos);
                        currentLabel = (tokens[1].Trim(), currentBinPos, lineNumber + 1);
                        break;
                    case "DATA":
                        if (
                            tokens.Length != 3 ||
                            string.IsNullOrWhiteSpace(tokens[1]) ||
                            string.IsNullOrWhiteSpace(tokens[2]))
                        {
                            throw new AssemblerException("Empty data name or no data!", null, lineNumber + 1, lines[lineNumber]);
                        }

                        var data = tokens[2].Trim();
                        byte[] binData;
                        CompleteCurrentLabel(labels, currentLabel, lineNumber - currentLabel.line, ref currentAsmSize, ref currentBinPos);
                        if (data[0] == '"')
                        {
                            binData = Encoding.ASCII.GetBytes(data.Trim('"'));
                        }
                        else
                        {
                            binData = new byte[data.Length / 2];
                            for (var i = 0; i < data.Length; i += 2)
                            {
                                try
                                {
                                    binData[0] = byte.Parse(data.Substring(i, 2));
                                }
                                catch (FormatException e)
                                {
                                    throw new AssemblerException("Invalid data!", e, lineNumber, lines[lineNumber]);
                                }
                            }
                        }

                        labels.Add(tokens[1].Trim(), new Label(lineNumber, 1, currentBinPos, binData, true));
                        currentLabel = (null, 0, 0);
                        currentBinPos += binData.Length;
                        break;
                    default:
                        if (currentLabel.name == null)
                        {
                            throw new AssemblerException("Assembler code without label.", null, lineNumber, lines[lineNumber]);
                        }

                        currentAsmSize += 2;
                        break;
                }
            }

            CompleteCurrentLabel(labels, currentLabel, lines.Length - currentLabel.line, ref currentAsmSize, ref currentBinPos);

            var image = new byte[currentBinPos];
            foreach (var label in labels.Values)
            {
                byte[] bin;
                if (label.IsData)
                {
                    bin = label.Data;
                }
                else
                {
                    var asm = lines
                        .Skip(label.StartLine)
                        .Take(label.SourceLength)
                        .Select(t => RemoveComments(t))
                        .Where(t => !string.IsNullOrWhiteSpace(RemoveComments(t)))
                        .Select(line =>
                        {
                            return line                            
                            .Split(' ')
                            .Where(t => !string.IsNullOrWhiteSpace(t))
                            .Select((t, i) =>
                            {
                                if (i != 0)
                                {
                                    if (labels.TryGetValue(t, out var matchedLabel))
                                    {
                                        return Convert.ToString(matchedLabel.Pos, 16);
                                    }
                                }

                                // Token was instruction name or was not label reference
                                return t;
                            })
                            .Aggregate((s1, s2) => s1 + ' ' + s2);
                        });

                    bin = AssembleBlock(asm);
                }

                Array.Copy(bin, 0, image, label.Pos, bin.Length);
            }

            return image;
        }

        public static byte[] AssembleBlock(IEnumerable<string> asmOnlySource)
        {
            var asm = new List<byte>();
            foreach (var line in asmOnlySource)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var op = new Op(line);
                op.ToBinaryOp(out var b1, out var b2);
                asm.Add(b1);
                asm.Add(b2);
            }

            return asm.ToArray();
        }

        public static string DisassembleBlock(byte[] binary)
        {
            var disasm = new StringBuilder();
            for (var i = 0; i < binary.Length; i += 2)
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

        private static void CompleteCurrentLabel(Dictionary<string, Label> labels, (string name, int pos, int line) currentLabel, int srcLength, ref int currentAsmSize, ref int currentBinPos)
        {
            if (currentLabel.name != null)
            {
                labels.Add(currentLabel.name, new Label(currentLabel.line, srcLength, currentLabel.pos, null, false));
                currentBinPos += currentAsmSize;
                currentAsmSize = 0;
            }
        }

        public struct Label
        {
            private int pos, startLine, sourceLength;
            private byte[] data;
            private bool isData;

            public Label(int startLine, int sourceLength, int pos, byte[] data, bool isData)
            {
                this.pos = pos;
                this.startLine = startLine;
                this.data = data;
                this.isData = isData;
                this.sourceLength = sourceLength;
            }

            public int Pos { get => this.pos; }
            public int StartLine { get => this.startLine; }
            public byte[] Data { get => this.data; }
            public bool IsData { get => this.isData; }
            public int SourceLength { get => this.sourceLength; }
        }
    }
}
