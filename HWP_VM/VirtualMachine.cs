namespace HWP_VM
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;


    public class VirtualMachine
    {
        public enum MachineState
        {
            Ready, Stopped, Exception
        }

        public enum OpCodes
        {
            NOP = 0x0, LOAD = 0x1, MOV = 0x2, ADD = 0x3, SUB = 0x4,
            MUL = 0x5, DIV = 0x6, PUSH = 0x7, POP = 0x8, JMP = 0x9,
            JIZ = 0xA, JIH = 0xB, JSR = 0xC, RTS = 0xD
        }

        private const int defaultMemSize = 4096;
        private const int defaultStackSize = 256;
        private const int defaultRegisterCnt = 16;

        private byte[] memory;
        private Stack<ushort> stack;
        private ushort[] generalRegisters;
        private ushort programCounter;
        private MachineState state;

        public MachineState State => this.state;
        public int ProgramCounter => this.programCounter;
        public int MemorySize => this.memory.Length;
        public VirtualMachine()
        {
            Reset();
        }

        public void Step()
        {
            if (this.state != MachineState.Ready)
            {
                throw new InvalidOperationException($"Execution can not continue because the machine is in a {state} state.");
            }

            var op = new Op(this.memory[this.programCounter], this.memory[this.programCounter + 1]);
            switch (op.OpCode)
            {
                case OpCodes.NOP:
                    break;
                case OpCodes.LOAD:
                    this.generalRegisters[0] = op.Value;
                    break;
                case OpCodes.MOV:
                    if (op.ToMemFlag && op.FromMemFlag)
                    {
                        this.state = MachineState.Exception;
                        return;
                    }

                    if (op.ToMemFlag)
                    {
                        WriteMemory(this.generalRegisters[op.IndexRegX], this.generalRegisters[op.IndexRegY]);
                    }
                    else if (op.FromMemFlag)
                    {
                        this.generalRegisters[op.IndexRegX] = ReadMemory(this.generalRegisters[op.IndexRegY]);
                    }
                    else
                    {
                        this.generalRegisters[op.IndexRegX] = this.generalRegisters[op.IndexRegY];
                    }
                    break;
                case OpCodes.ADD:
                    this.generalRegisters[op.IndexRegX] = unchecked((ushort)(this.generalRegisters[op.IndexRegX] + this.generalRegisters[op.IndexRegY]));
                    break;
                case OpCodes.SUB:
                    this.generalRegisters[op.IndexRegX] = unchecked((ushort)(this.generalRegisters[op.IndexRegX] - this.generalRegisters[op.IndexRegY]));
                    break;
                case OpCodes.MUL:
                    this.generalRegisters[op.IndexRegX] = unchecked((ushort)(this.generalRegisters[op.IndexRegX] * this.generalRegisters[op.IndexRegY]));
                    break;
                case OpCodes.DIV:
                    if (op.ToMemFlag)
                    {
                        this.generalRegisters[op.IndexRegX] = unchecked((ushort)(this.generalRegisters[op.IndexRegX] % this.generalRegisters[op.IndexRegY]));
                    }
                    else
                    {
                        this.generalRegisters[op.IndexRegX] = unchecked((ushort)(this.generalRegisters[op.IndexRegX] / this.generalRegisters[op.IndexRegY]));
                    }
                    break;
                case OpCodes.PUSH:
                    if (this.stack.Count >= defaultStackSize)
                    {
                        this.state = MachineState.Exception;
                        return;
                    }

                    this.stack.Push(this.generalRegisters[op.IndexRegX]);
                    break;
                case OpCodes.POP:
                    if (this.stack.Count <= 0)
                    {
                        this.state = MachineState.Exception;
                        return;
                    }

                    this.generalRegisters[op.IndexRegX] = this.stack.Pop();
                    break;
                case OpCodes.JMP:
                    this.programCounter = (ushort)(op.Value - 1);
                    break;
                case OpCodes.JIZ:
                    if (this.generalRegisters[0] == 0)
                    {
                        JumpToAddress(op.Value);
                    }
                    break;
                case OpCodes.JIH:
                    if (this.generalRegisters[0] == 0)
                    {
                        JumpToAddress(op.Value);
                    }
                    break;
                case OpCodes.JSR:
                    if (this.stack.Count >= defaultStackSize)
                    {
                        this.state = MachineState.Exception;
                        return;
                    }

                    this.stack.Push(programCounter);
                    JumpToAddress(op.Value);
                    break;
                case OpCodes.RTS:
                    if (this.stack.Count <= 0)
                    {
                        this.state = MachineState.Stopped;
                        return;
                    }

                    JumpToAddress(this.stack.Pop());
                    break;
                default:
                    this.state = MachineState.Exception;
                    return;
            }

            this.programCounter += 2;
        }

        public void Reset()
        {
            this.memory = new byte[defaultMemSize];
            this.generalRegisters = new ushort[defaultRegisterCnt];
            this.stack = new Stack<ushort>(defaultStackSize);
        }

        public void WriteMemory(int offset, byte[] data)
        {
            Array.Copy(data, 0, this.memory, offset, data.Length);
        }

        public void WriteMemory(int offset, ushort value)
        {
            this.memory[offset] = (byte)(value >> 8);
            this.memory[offset + 1] = (byte)(value & 0xFF);
        }

        public byte[] ReadMemory(int offset, int length)
        {
            if (offset < 0 || length + offset > this.memory.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            var res = new byte[length];
            Array.Copy(this.memory, offset, res, 0, length);
            return res;
        }

        public ushort ReadMemory(int offset)
        {
            return (ushort)(this.memory[offset] << 8 | this.memory[offset + 1]);
        }

        public ushort[] GetRegisters()
        {
            var regs = new ushort[defaultRegisterCnt];
            this.generalRegisters.CopyTo(regs, 0);
            return regs;
        }

        private void JumpToAddress(int address)
        {
            this.programCounter = (ushort)(address - 2);
        }
    }
}
