namespace HWP_VM
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;


    public struct Op
    {
        private const ushort IndexRX = 0xF;
        private const ushort IndexRY = 0xF0;
        private const ushort ToMem = 0x100;
        private const ushort FromMem = 0x200;

        private const string ToMemToken = "ToMem";
        private const string FromMemToken = "FromMem";

        private ushort val;
        private VirtualMachine.OpCodes opCode;

        public Op(string sourceLine)
        {
            var tokens = sourceLine.Split(' ');
            if (!Enum.TryParse(tokens[0], out opCode))
            {
                throw new FormatException($"Unknown OP code in \"{sourceLine}\".");
            }

            val = 0;

            if (tokens[0] == "MOD")
            {
                tokens[0] = "DIV";
                ToMemFlag = true;
            }
            int expectedTokenCount = 1;
            switch (opCode)
            {
                case VirtualMachine.OpCodes.NOP:
                case VirtualMachine.OpCodes.RTS:
                    break;
                case VirtualMachine.OpCodes.JMP:
                case VirtualMachine.OpCodes.JIZ:
                case VirtualMachine.OpCodes.JIH:
                case VirtualMachine.OpCodes.JSR:
                case VirtualMachine.OpCodes.LOAD:
                    val = ushort.Parse(tokens[1]);
                    expectedTokenCount = 2;
                    break;
                case VirtualMachine.OpCodes.MOV:
                    SetRegisterIndices(tokens[1], tokens[2]);
                    if (tokens.Length == 3 || string.IsNullOrWhiteSpace(tokens[3]))
                    {
                        expectedTokenCount = 3;
                    }
                    else
                    {
                        expectedTokenCount = 4;
                        if (tokens[3] == ToMemToken)
                        {
                            ToMemFlag = true;
                        }
                        else if (tokens[3] == FromMemToken)
                        {
                            FromMemFlag = true;
                        }
                    }
                    break;
                case VirtualMachine.OpCodes.ADD:
                case VirtualMachine.OpCodes.SUB:
                case VirtualMachine.OpCodes.MUL:
                case VirtualMachine.OpCodes.DIV:
                    expectedTokenCount = 3;
                    SetRegisterIndices(tokens[1], tokens[2]);
                    break;
                case VirtualMachine.OpCodes.PUSH:
                case VirtualMachine.OpCodes.POP:
                    expectedTokenCount = 2;
                    SetRegisterIndices(tokens[1], null);
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (tokens.Length != expectedTokenCount)
            {
                var unexpected = "";
                tokens.ToList().ForEach(t => unexpected += t + " ");
                throw new FormatException($"Unexpected tokens \"{unexpected}\" in \"{sourceLine}\".");
            }
        }

        public Op(ushort binaryOp)
        {
            var opCode = binaryOp & 0xF;
            this.val = (ushort)((binaryOp & 0xFFF0) >> 4);
            if (!Enum.IsDefined(typeof(VirtualMachine.OpCodes), opCode))
            {
                throw new ArgumentException($"Unknown OpCode : 0x{opCode:X} in {binaryOp:X}.");
            }

            this.opCode = (VirtualMachine.OpCodes)opCode;

            if (this.opCode == VirtualMachine.OpCodes.MOV && ToMemFlag && FromMemFlag)
            {
                throw new ArgumentException("False MOV flag usage.");
            }
        }

        public Op(byte bin1, byte bin2)
            : this((ushort)(bin1 << 8 | bin2))
        {

        }

        public VirtualMachine.OpCodes OpCode { get { return opCode; } }

        public byte OpCodeBin
        {
            get
            {
                return (byte)opCode;
            }
        }

        public ushort Value
        {
            get
            {
                return val;
            }
            private set
            {
                val = value;
            }
        }

        public byte IndexRegX
        {
            get
            {
                return (byte)((val & IndexRX));
            }

            private set
            {
                if (value > 0xF)
                {
                    throw new ArgumentException();
                }
                val &= unchecked((ushort)~IndexRX);
                val |= (byte)(value);
            }
        }

        public byte IndexRegY
        {
            get
            {
                return (byte)((val & IndexRY) >> 4);
            }
            private set
            {
                if (value > 0xF)
                {
                    throw new ArgumentException();
                }
                val &= unchecked((ushort)~IndexRY);
                val |= (byte)(value << 4);
            }
        }

        public bool ToMemFlag
        {
            get
            {
                return (val & ToMem) == ToMem;
            }

            private set
            {
                val &= unchecked((ushort)~ToMem);
                val |= (byte)((value ? 1 : 0) << 9);
            }
        }

        public bool FromMemFlag
        {
            get
            {
                return
                    (val & FromMem) == FromMem;
            }

            private set
            {
                val &= unchecked((ushort)~FromMem);
                val |= (byte)((value ? 1 : 0) << 10);
            }
        }

        public ushort BinaryOp
        {
            get
            {
                return (ushort)(OpCodeBin | (val << 4));
            }
        }

        public void ToBinaryOp(out byte b1, out byte b2)
        {
            b1 = (byte)(BinaryOp >> 8);
            b2 = (byte)(BinaryOp & 0xFF);
        }

        public override string ToString()
        {
            StringBuilder opStr = new StringBuilder();
            if (opCode == VirtualMachine.OpCodes.DIV && ToMemFlag)
            {
                opStr.Append("MOD");
            }
            else
            {
                opStr.Append(opCode.ToString());
            }

            opStr.Append(" ");
            switch (opCode)
            {
                case VirtualMachine.OpCodes.RTS:
                case VirtualMachine.OpCodes.NOP:
                    break;
                case VirtualMachine.OpCodes.MOV:
                    opStr.Append($"{IndexRegX:X} {IndexRegY:X} ");
                    if (ToMemFlag)
                    {
                        opStr.Append(ToMemToken);
                    }
                    else if (FromMemFlag)
                    {
                        opStr.Append(FromMemToken);
                    }
                    break;
                case VirtualMachine.OpCodes.ADD:
                case VirtualMachine.OpCodes.SUB:
                case VirtualMachine.OpCodes.MUL:
                case VirtualMachine.OpCodes.DIV:
                    opStr.Append($"{IndexRegX:X} {IndexRegY:X}");
                    break;
                case VirtualMachine.OpCodes.PUSH:
                case VirtualMachine.OpCodes.POP:
                    opStr.Append($"{IndexRegX:X}");
                    break;
                case VirtualMachine.OpCodes.LOAD:
                case VirtualMachine.OpCodes.JMP:
                case VirtualMachine.OpCodes.JIZ:
                case VirtualMachine.OpCodes.JIH:
                case VirtualMachine.OpCodes.JSR:
                    opStr.Append($"{Value:X}");
                    break;
                default:
                    throw new NotImplementedException();
            }

            return opStr.ToString();
        }

        private void SetRegisterIndices(string rx, string ry = null)
        {
            IndexRegX = byte.Parse(rx);
            if (!string.IsNullOrWhiteSpace(ry))
            {
                IndexRegY = byte.Parse(ry);
            }

            if (IndexRegX > 0xF || IndexRegY > 0xF)
            {
                throw new InvalidOperationException("Register Index out of bounds!");
            }
        }
    }
}
