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
        private int programCounter;
        private MachineState state;

        public MachineState State
        {
            get
            {
                return state;
            }
        }

        public int ProgramCounter
        {
            get
            {
                return programCounter;
            }
        }

        public int MemorySize
        {
            get { return memory.Length; }
        }

        public VirtualMachine()
        {
            Reset();
        }

        public void Step()
        {
            if (state != MachineState.Ready)
            {
                throw new InvalidOperationException($"Execution can not continue because the machine is in a {state} state.");
            }

            var op = new Op(memory[programCounter], memory[programCounter + 1]);
            switch (op.OpCode)
            {
                case OpCodes.NOP:
                    break;
                case OpCodes.LOAD:
                    generalRegisters[0] = op.Value;
                    break;
                case OpCodes.MOV:
                    if (op.ToMemFlag && op.FromMemFlag)
                    {
                        state = MachineState.Exception;
                        return;
                    }

                    if (op.ToMemFlag)
                    {
                        WriteMemory(generalRegisters[op.IndexRegX], generalRegisters[op.IndexRegY]);
                    }
                    else if (op.FromMemFlag)
                    {
                        generalRegisters[op.IndexRegX] = ReadMemory(generalRegisters[op.IndexRegY]);
                    }
                    else
                    {
                        generalRegisters[op.IndexRegX] = generalRegisters[op.IndexRegY];
                    }
                    break;
                case OpCodes.ADD:
                    generalRegisters[op.IndexRegX] = unchecked((ushort)(generalRegisters[op.IndexRegX] + generalRegisters[op.IndexRegY]));
                    break;
                case OpCodes.SUB:
                    generalRegisters[op.IndexRegX] = unchecked((ushort)(generalRegisters[op.IndexRegX] - generalRegisters[op.IndexRegY]));
                    break;
                case OpCodes.MUL:
                    generalRegisters[op.IndexRegX] = unchecked((ushort)(generalRegisters[op.IndexRegX] * generalRegisters[op.IndexRegY]));
                    break;
                case OpCodes.DIV:
                    if (op.ToMemFlag)
                    {
                        generalRegisters[op.IndexRegX] = unchecked((ushort)(generalRegisters[op.IndexRegX] % generalRegisters[op.IndexRegY]));
                    }
                    else
                    {
                        generalRegisters[op.IndexRegX] = unchecked((ushort)(generalRegisters[op.IndexRegX] / generalRegisters[op.IndexRegY]));
                    }
                    break;
                case OpCodes.PUSH:
                    if (stack.Count >= defaultStackSize)
                    {
                        state = MachineState.Exception;
                        return;
                    }

                    stack.Push(generalRegisters[op.IndexRegX]);
                    break;
                case OpCodes.POP:
                    if (stack.Count <= 0)
                    {
                        state = MachineState.Exception;
                        return;
                    }

                    generalRegisters[op.IndexRegX] = stack.Pop();
                    break;
                case OpCodes.JMP:
                    programCounter = op.Value - 1;
                    break;
                case OpCodes.JIZ:
                    if (generalRegisters[0] == 0)
                    {
                        JumpToAddress(op.Value);
                    }
                    break;
                case OpCodes.JIH:
                    if (generalRegisters[0] == 0)
                    {
                        JumpToAddress(op.Value);
                    }
                    break;
                case OpCodes.JSR:
                    if (stack.Count >= defaultStackSize)
                    {
                        state = MachineState.Exception;
                        return;
                    }

                    stack.Push(generalRegisters[op.Value]);
                    JumpToAddress(generalRegisters[op.Value]);
                    break;
                case OpCodes.RTS:
                    if (stack.Count <= 0)
                    {
                        state = MachineState.Stopped;
                        return;
                    }

                    JumpToAddress(stack.Pop());
                    break;
                default:
                    state = MachineState.Exception;
                    return;
            }

            programCounter += 2;
        }

        public void Reset()
        {
            memory = new byte[defaultMemSize];
            generalRegisters = new ushort[defaultRegisterCnt];
            stack = new Stack<ushort>(defaultStackSize);
        }

        public void WriteMemory(int offset, byte[] data)
        {
            Array.Copy(data, 0, memory, offset, data.Length);
        }

        public void WriteMemory(int offset, ushort value)
        {
            memory[offset] = (byte)(value >> 8);
            memory[offset + 1] = (byte)(value & 0xFF);
        }

        public byte[] ReadMemory(int offset, int length)
        {
            if (offset < 0 || length + offset > memory.Length)
            {
                throw new ArgumentOutOfRangeException();
            }

            var res = new byte[length];
            Array.Copy(memory, offset, res, 0, length);
            return res;
        }

        public ushort ReadMemory(int offset)
        {
            return (ushort)(memory[offset] << 8 | memory[offset + 1]);
        }

        public ushort[] GetRegisters()
        {
            var regs = new ushort[defaultRegisterCnt];
            generalRegisters.CopyTo(regs, 0);
            return regs;
        }

        private void JumpToAddress(int address)
        {
            programCounter = address - 2;
        }
    }
}
