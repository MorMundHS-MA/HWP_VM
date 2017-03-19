

namespace HWP_VM
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
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
            vm = new VirtualMachine();
            var test = @"
LOAD 1
MOV 1 0
LOAD 2
ADD 0 1
RTS";
            var asm = Assembler.Assemble(test);
            vm.WriteMemory(0, asm);
        }

        private async void Step_Click(object sender, RoutedEventArgs e)
        {
            AllowRunButtons(false);
            Task<ushort[]> step = new Task<ushort[]>(() =>
            {
                lock (vm)
                {
                    vm.Step();
                    return vm.GetRegisters();
                }
            });

            step.Start();
            var regs = await step;
            RegisterInfo.Clear();
            RegisterInfo.Add($"State : {vm.State}");
            RegisterInfo.Add($"PC : {vm.ProgramCounter}");
            for (int i = 0; i < 16; i++)
            {
                RegisterInfo.Add($"R{i}:{regs[i]}");
            }
            if (vm.State == VirtualMachine.MachineState.Ready)
            {
                AllowRunButtons(true);
            }
        }

        private void AllowRunButtons(bool allow)
        {
            if (allow)
            {
                BtnStep.IsEnabled = true;
            }
            else
            {
                BtnStep.IsEnabled = false;
            }
        }

        private void textBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            int offset;
            bool canParse = int.TryParse(e.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset);
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

        private async void textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (vm == null)
            {
                return;
            }

            int offset;
            bool canParse = int.TryParse(textBox.Text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out offset);
            if (!canParse)
            {
                canParse = int.TryParse(textBox.Text, out offset);
            }

            Task<byte[]> getMem = new Task<byte[]>(() =>
            {
                lock (vm)
                {
                    return vm.ReadMemory(offset, vm.MemorySize - offset);
                }
            });
            getMem.Start();
            var mem = await getMem;
            MemoryInfo.Clear();
            foreach (var b in mem)
            {
                MemoryInfo.Add(b);
            }
        }
    }
}
