

namespace HWP_VM
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Documents;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Windows.Shapes;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private VirtualMachine vm;
        public ObservableCollection<string> RegisterInfo { get; private set; } = new ObservableCollection<string>() { "Idle" };
        public ObservableCollection<byte> MemoryInfo { get; private set; } = new ObservableCollection<byte>() { };

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.vm = new VirtualMachine();
            var test = File.ReadAllText("asm/Fibonacci asm.txt");
            var asm = Assembler.Assemble(test);
            this.vm.WriteMemory(0, asm);
        }

        private async void Step_Click(object sender, RoutedEventArgs e)
        {
            AllowRunButtons(false);
            var step = new Task<ushort[]>(() =>
            {
                lock (this.vm)
                {
                    this.vm.Step();
                    return this.vm.GetRegisters();
                }
            });

            step.Start();
            var regs = await step;
            this.RegisterInfo.Clear();
            this.RegisterInfo.Add($"State : {vm.State}");
            this.RegisterInfo.Add($"PC : {vm.ProgramCounter}");
            this.RegisterInfo.Add($"Next Instr : {new Op(vm.ReadMemory(vm.ProgramCounter)).ToString()}");
            this.RegisterInfo.Add($"Stack ptr : {vm.StackSize}");
            this.RegisterInfo.Add($"Stack top : {(vm.StackSize != 0 ? vm.StackTop.ToString() : "Empty")}");

            for (var i = 0; i < 16; i++)
            {
                this.RegisterInfo.Add($"R{i}:{regs[i]}");
            }
            if (this.vm.State == VirtualMachine.MachineState.Ready)
            {
                AllowRunButtons(true);
            }
        }

        private void AllowRunButtons(bool allow)
        {
            if (allow)
            {
                this.BtnStep.IsEnabled = true;
            }
            else
            {
                this.BtnStep.IsEnabled = false;
            }
        }

        private void MemViewOffset_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var canParse = int.TryParse(e.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var offset);
            if (!canParse)
            {
                canParse = int.TryParse(e.Text, out offset);
            }

            if (!canParse && e.Text != "0x")
            {
                e.Handled = true;
                return;
            }
        }

        private async void MemViewOffset_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this.vm == null)
            {
                return;
            }

            var canParse = int.TryParse(this.MemViewOffset.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var offset);
            if (!canParse)
            {
                canParse = int.TryParse(this.MemViewOffset.Text, out offset);
            }

            var getMem = new Task<byte[]>(() =>
            {
                lock (this.vm)
                {
                    return this.vm.ReadMemory(offset, this.vm.MemorySize - offset);
                }
            });

            getMem.Start();
            var mem = await getMem;
            this.MemoryInfo.Clear();
            foreach (var b in mem)
            {
                this.MemoryInfo.Add(b);
            }
        }
    }
}
