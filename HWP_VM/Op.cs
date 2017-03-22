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
            if (!Enum.TryParse(tokens[0], out this.opCode))
            {
                throw new FormatException($"Unknown OP code in \"{sourceLine}\".");
            }

            this.val = 0;

            if (tokens[0] == "MOD")
            {
                tokens[0] = "DIV";
                this.ToMemFlag = true;
            }

            var expectedTokenCount = 1;
            switch (this.opCode)
            {
                case VirtualMachine.OpCodes.NOP:
                case VirtualMachine.OpCodes.RTS:
                    break;
                case VirtualMachine.OpCodes.JMP:
                case VirtualMachine.OpCodes.JIZ:
                case VirtualMachine.OpCodes.JIH:
                case VirtualMachine.OpCodes.JSR:
                case VirtualMachine.OpCodes.LOAD:
                    this.val = ushort.Parse(tokens[1], System.Globalization.NumberStyles.HexNumber);
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
                            this.ToMemFlag = true;
                        }
                        else if (tokens[3] == FromMemToken)
                        {
                            this.FromMemFlag = true;
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

            if (this.opCode == VirtualMachine.OpCodes.MOV && this.ToMemFlag && this.FromMemFlag)
            {
                throw new ArgumentException("False MOV flag usage.");
            }
        }

        public Op(byte bin1, byte bin2)
            : this((ushort)(bin1 << 8 | bin2))
        {

        }

        public VirtualMachine.OpCodes OpCode => this.opCode;
        public byte OpCodeBin => (byte)this.opCode;

        public ushort Value
        {
            get => this.val;
            private set => this.val = value;
        }

        public byte IndexRegX
        {
            get => (byte)((this.val & IndexRX));

            private set
            {
                if (value > 0xF)
                {
                    throw new ArgumentException();
                }
                this.val &= unchecked((ushort)~IndexRX);
                this.val |= (byte)(value);
            }
        }

        public byte IndexRegY
        {
            get => (byte)((this.val & IndexRY) >> 4);
            private set
            {
                if (value > 0xF)
                {
                    throw new ArgumentException();
                }
                this.val &= unchecked((ushort)~IndexRY);
                this.val |= (byte)(value << 4);
            }
        }

        public bool ToMemFlag
        {
            get => (this.val & ToMem) == ToMem;

            private set
            {
                this.val &= unchecked((ushort)~ToMem);
                this.val |= (byte)((value ? 1 : 0) << 9);
            }
        }

        public bool FromMemFlag
        {
            get => (this.val & FromMem) == FromMem;

            private set
            {
                this.val &= unchecked((ushort)~FromMem);
                this.val |= (byte)((value ? 1 : 0) << 10);
            }
        }

        public ushort BinaryOp => (ushort)(this.OpCodeBin | (this.val << 4));

        public void ToBinaryOp(out byte b1, out byte b2)
        {
            b1 = (byte)(this.BinaryOp >> 8);
            b2 = (byte)(this.BinaryOp & 0xFF);
        }

        public override string ToString()
        {
            var opStr = new StringBuilder();
            if (this.opCode == VirtualMachine.OpCodes.DIV && this.ToMemFlag)
            {
                opStr.Append("MOD");
            }
            else
            {
                opStr.Append(this.opCode.ToString());
            }

            opStr.Append(" ");
            switch (this.opCode)
            {
                case VirtualMachine.OpCodes.RTS:
                case VirtualMachine.OpCodes.NOP:
                    break;
                case VirtualMachine.OpCodes.MOV:
                    opStr.Append($"{IndexRegX:X} {IndexRegY:X} ");
                    if (this.ToMemFlag)
                    {
                        opStr.Append(ToMemToken);
                    }
                    else if (this.FromMemFlag)
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
            this.IndexRegX = byte.Parse(rx);
            if (!string.IsNullOrWhiteSpace(ry))
            {
                this.IndexRegY = byte.Parse(ry);
            }

            if (this.IndexRegX > 0xF || this.IndexRegY > 0xF)
            {
                throw new InvalidOperationException("Register Index out of bounds!");
            }
        }
    }
}
