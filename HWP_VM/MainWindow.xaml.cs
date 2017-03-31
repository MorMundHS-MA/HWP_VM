

namespace HWP_VM
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
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
        private const string defSrcPath = "asm/Fibonacci asm.txt";
        private VirtualMachine vm;
        private Profiler profiler;
        private bool isVmInitialized = false;

        public ObservableCollection<string> RegisterInfo { get; private set; } = new ObservableCollection<string>() { "Idle" };
        public ObservableCollection<byte> MemoryInfo { get; private set; } = new ObservableCollection<byte>();
        public ObservableCollection<DebugLine> DebugInfo { get; private set; } = new ObservableCollection<DebugLine>();

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            this.vm = new VirtualMachine();
        }

        private async void Step_Click(object sender, RoutedEventArgs e)
        {
            AllowRunButtons(false);
            var currentLine = this.vm.ProgramCounter / 2;
            if (!this.isVmInitialized)
            {
                lock (this.vm)
                {
                    InitVmMemory(this.ViewSrc.Text);
                }
            }

            var step = Task.Run(() =>
            {
                lock (this.vm)
                {
                    this.vm.Step();
                    this.profiler.Step(this.vm.ProgramCounter);
                    return this.vm.GetRegisters();
                }
            });

            var regs = await step;
            SetRegisterInfo(regs);
            this.DebugInfo[currentLine].ProfilerHitCount++;

            if (this.vm.State == VirtualMachine.MachineState.Ready)
            {
                AllowRunButtons(true);
            }
        }

        private async void Run_Click(object sender, RoutedEventArgs e)
        {
            AllowRunButtons(false);
            if (!this.isVmInitialized)
            {
                lock (this.vm)
                {
                    InitVmMemory(this.ViewSrc.Text);
                }
            }

            var profilerCache = new int[this.DebugInfo.Count];
            var breakpointCache = this.DebugInfo.Select(d => d.HasBreakpoint).ToArray();
            var run = Task.Run(() =>
            {
                lock (this.vm)
                {
                    var firstLoop = true;
                    while (
                        this.vm.State == VirtualMachine.MachineState.Ready && 
                        (firstLoop || !breakpointCache[this.vm.ProgramCounter / 2]))
                    {
                        firstLoop = false;
                        profilerCache[this.vm.ProgramCounter / 2]++;
                        this.profiler.Step(this.vm.ProgramCounter);
                        this.vm.Step();
                    }

                    return this.vm.GetRegisters();
                }
            });

            var regs = await run;
            for (var i = 0; i < breakpointCache.Length; i++)
            {
                this.DebugInfo[i].ProfilerHitCount += profilerCache[i];
            }

            SetRegisterInfo(regs);

            if (this.vm.State == VirtualMachine.MachineState.Ready)
            {
                AllowRunButtons(true);
            }
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            var reset = Task.Run(() =>
            {
                lock (this.vm)
                {
                    this.vm.Reset();

                    return this.vm.GetRegisters();
                }
            });

            var regs = await reset;
            SetRegisterInfo(regs);
            foreach (var dbg in this.DebugInfo)
            {
                dbg.ProfilerHitCount = 0;
            }

            if (this.vm.State == VirtualMachine.MachineState.Ready)
            {
                AllowRunButtons(true);
            }
        }

        private async void Load_Click(object sender, RoutedEventArgs e)
        {
            AllowIOButtons(false);
            AllowRunButtons(false);
            this.vm.Reset();
            this.ViewSrc.Text = await Task.Run(() => File.ReadAllText(defSrcPath));
            AllowIOButtons(true);
            AllowRunButtons(true);
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            AllowIOButtons(false);
            var source = this.ViewSrc.Text;
            await Task.Run(() => File.WriteAllText(defSrcPath, source));
            AllowIOButtons(true);
        }

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void InitVmMemory(string src)
        {
            var asm = Assembler.Assemble(src, out var disasm);
            lock (this.vm)
            {
                this.vm.WriteMemory(0, asm);
            }

            this.DebugInfo.Clear();
            this.profiler = new Profiler();
            var i = -2; // For zero based numbering
            disasm.ForEach(op => this.DebugInfo.Add(new DebugLine(op.ToString(), i += 2, this.profiler)));
            this.isVmInitialized = true;
        }

        private void AllowRunButtons(bool allow)
        {
            if (allow)
            {
                this.BtnStep.IsEnabled = true;
                this.BtnRun.IsEnabled = true;
                this.BtnReset.IsEnabled = this.isVmInitialized;
            }
            else
            {
                this.BtnRun.IsEnabled = false;
                this.BtnStep.IsEnabled = false;
                this.BtnReset.IsEnabled = false;
            }
        }

        private void AllowIOButtons(bool allow)
        {
            if (allow)
            {
                /* this.BtnExport.IsEnabled = true;
                 this.BtnImport.IsEnabled = true;*/
                this.BtnSaveSrc.IsEnabled = true;
                this.BtnLoadSrc.IsEnabled = true;
            }
            else
            {
                /*this.BtnExport.IsEnabled = false;
                this.BtnImport.IsEnabled = false;*/
                this.BtnSaveSrc.IsEnabled = false;
                this.BtnLoadSrc.IsEnabled = false;
            }
        }

        private void SetRegisterInfo(ushort[] regs)
        {
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

        private static double Lerp(double v0, double v1, double t)
        {
            return v0 + t * (v1 - v0);
        }

        public class DebugLine : INotifyPropertyChanged
        {
            private Profiler profiler;
            private bool hasBreakPoint;
            private string source;
            private int profilerHitCount;
            private int lineNumber;

            public event PropertyChangedEventHandler PropertyChanged;

            public bool HasBreakpoint
            {
                get => hasBreakPoint;
                set
                {
                    if (this.hasBreakPoint != value)
                    {
                        this.hasBreakPoint = value;
                        OnPropertyChanged("HasBreakpoint");
                    }
                }
            }
            public string SourceLine
            {
                get => source;
                set
                {
                    if (this.source != value)
                    {
                        this.source = value;
                        OnPropertyChanged("SourceLine");
                    }
                }
            }
            public int ProfilerHitCount
            {
                get => profilerHitCount;
                set
                {
                    if (this.profilerHitCount != value)
                    {
                        this.profilerHitCount = value;
                        OnPropertyChanged("ProfilerHitCount");
                        HitCountChanged();
                    }
                }
            }


            public double ProfilerPercentage => ((double)this.ProfilerHitCount / Math.Max(1, this.profiler.ProfilerTotal)) * 100;
            public Color ProfilerPercentageColor
            {
                get
                {
                    if (this.profilerHitCount > 0)
                    {
                        var redHitCount = (this.profiler.ProfilerTotal * 3) / this.profiler.ExecutedAddressesTotal;
                        var hue = Lerp(0.33, 0, (double)this.profilerHitCount / redHitCount);
                        return FromHSLA(hue, 0.5, 0.5, 1);
                    }
                    else
                    {
                        return Color.FromRgb(50, 50, 50);
                    }
                }
            }

            public int LineNumber
            {
                get => this.lineNumber; set
                {
                    if (this.lineNumber != value)
                    {
                        this.lineNumber = value;
                        OnPropertyChanged("LineNumber");
                    }
                }
            }

            public DebugLine(string sourceLine, int lineNumber, Profiler profiler)
            {
                this.profiler = profiler;
                this.lineNumber = lineNumber;
                this.HasBreakpoint = false;
                this.SourceLine = sourceLine;
                this.ProfilerHitCount = 0;
                profiler.PropertyChanged += Profiler_PropertyChanged;
            }

            private void Profiler_PropertyChanged(object sender, PropertyChangedEventArgs e)
            {
                HitCountChanged();
            }

            private void OnPropertyChanged(string propName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            }

            private void HitCountChanged()
            {
                OnPropertyChanged("ProfilerPercentage");
                OnPropertyChanged("ProfilerPercentageColor");
            }
        }

        public class Profiler : INotifyPropertyChanged
        {
            private int profilerTotal;
            private HashSet<int> executedAddresses = new HashSet<int>();
            public int ProfilerTotal => this.profilerTotal;
            public int ExecutedAddressesTotal => this.executedAddresses.Count;            

            public event PropertyChangedEventHandler PropertyChanged;
            public int GetProfilerTotal()
            {
                return this.profilerTotal;
            }

            public void Step(int programCounter)
            {
                this.profilerTotal++;
                if (this.executedAddresses.Add(programCounter))
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ExecutedAddressesTotal"));
                } 

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ProfilerTotal"));
            }
        }

        /// <summary>
        /// Given H,S,L,A in range of 0-1
        /// Returns a Color in the range of 0-255
        /// </summary>
        /// <param name="hue"></param>
        /// <param name="saturation"></param>
        /// <param name="luminosity"></param>
        /// <param name="alpha"></param>
        /// <returns></returns>
        public static Color FromHSLA(double hue, double saturation, double luminosity, double alpha)
        {
            double v;
            double r, g, b;
            Math.Min(1, alpha = 1.0);

            r = 0L;   // default to gray
            g = 0L;
            b = 0L;
            v = (luminosity <= 0.5) ? (luminosity * (1.0 + saturation)) : (luminosity + saturation - luminosity * saturation);
            if (v > 0)
            {
                double m;
                double sv;
                int sextant;
                double fract, vsf, mid1, mid2;

                m = luminosity + luminosity - v;
                sv = (v - m) / v;
                hue *= 6.0;
                sextant = (int)hue;
                fract = hue - sextant;
                vsf = v * sv * fract;
                mid1 = m + vsf;
                mid2 = v - vsf;
                switch (sextant)
                {
                    case 0:
                        r = v;
                        g = mid1;
                        b = m;
                        break;
                    case 1:
                        r = mid2;
                        g = v;
                        b = m;
                        break;
                    case 2:
                        r = m;
                        g = v;
                        b = mid1;
                        break;
                    case 3:
                        r = m;
                        g = mid2;
                        b = v;
                        break;
                    case 4:
                        r = mid1;
                        g = m;
                        b = v;
                        break;
                    case 5:
                        r = v;
                        g = m;
                        b = mid2;
                        break;
                }
            }

            return Color.FromScRgb((float)alpha, (float)r, (float)g, (float)b);
        }
    }
}
