using Prism.Commands;
using Prism.Mvvm;
using System.Timers;
using System.Windows;

namespace ModBusTerminal
{
    public class MainVM : BindableBase
    {
        Timer tm = new Timer(5000);
        readonly MyMathModel _model = new MyMathModel();
        public MainVM()
        {
            _model.PropertyChanged += (s, e) => { RaisePropertyChanged(e.PropertyName); };
            Refresh_list = new DelegateCommand(_model.Refresh_List);
            Send_command = new DelegateCommand<string>(i => { if(i != null) _model.Console_dialog(i); Console_text = null; RaisePropertyChanged("Console_text"); });
            OpenFirmware = new DelegateCommand(_model.Open_Firmware);
            Scan_switch = new DelegateCommand<string>(i => { if (i != null) _model.Scan_switch(i); });
            tm.Enabled = true;
            tm.Elapsed += new ElapsedEventHandler(_model.TimerElapsedEventHandler);
        }
        public string _selecteditem;
        public string SelectedItem
        {
            get
            {
                return _selecteditem;
            }
            set
            {
                _selecteditem = value;
                if (_selecteditem != null) _model.Port_choised(_selecteditem);
                RaisePropertyChanged("Port_state");
            }
        }



        public string Port_state { get { return _model._port_state; } set { RaisePropertyChanged("Port_state"); } }
        public string[] ListPorts => _model.FindPorts;
        public string Terminal => _model.output;
        public string Scan_state => _model.scan_state;
        public string Console_text { get; set; }
        public DelegateCommand Refresh_list { get; }
        public DelegateCommand<string> Send_command { get; }
        public DelegateCommand<string> Scan_switch { get; }
        public DelegateCommand OpenFirmware { get; }
        

    }

}
