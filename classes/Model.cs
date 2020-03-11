using Microsoft.Win32;
using NModbus;
using NModbus.Serial;
using Prism.Mvvm;
using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace ModBusTerminal
{

    public class MyMathModel : BindableBase
    {

        SerialPort port = new SerialPort();

        SerialPortAdapter adapter;
        ModbusFactory factory;
        IModbusMaster master;

        public string[] FindPorts = SerialPort.GetPortNames();
        public string output, _port_state, scan_state = "Wait", adress;
        int baudrate = 0;
        byte[] _File;
        enum State
        {
            wait,
            send_reset,
            wait_boot_enter,
            sending_firm,
            calc_all,
        }
        State state = State.wait;
        byte flash_addr;
        ushort st_reg;

        public void Refresh_List()
        {
            FindPorts = SerialPort.GetPortNames();
            RaisePropertyChanged("ListPorts");
        }

        public void Port_choised(string arg)
        {
            if (port.PortName != null)
            {
                try
                {
                    if (port.PortName == arg)
                    {
                        if (!port.IsOpen) port.Open();
                        output += "Port " + port.PortName + " opened" + "\n";
                        RaisePropertyChanged("Terminal");
                    }
                    else
                    {
                        if (port.IsOpen) port.Close();
                        port.PortName = arg;
                        port.Open();
                    }
                    _port_state = port.PortName + " opened";
                    if (baudrate != 0) port.BaudRate = baudrate;
                    port.Handshake = Handshake.None;
                    adapter = new SerialPortAdapter(port);
                    factory = new ModbusFactory();
                    master = factory.CreateRtuMaster(adapter);
                    master.Transport.ReadTimeout = 300;
                    master.Transport.WriteTimeout = 1000;
                    output += "Master created " + port.BaudRate.ToString() + "\n";
                    RaisePropertyChanged("Terminal");
                }
                catch (System.IO.IOException)
                {
                    Refresh_List();
                    _port_state = arg + " was lost";
                    output += "Master create error: " + "\n";
                    RaisePropertyChanged("Terminal");
                }
            }
            else
            {
                output += "Port not choised" + "\n";
                RaisePropertyChanged("Terminal");
            }
            RaisePropertyChanged("Port_state");
        }

        public ushort Read_reg(byte addr, ushort reg_addr, ushort num, string str, bool need = true)
        {
            ushort one_reg = 0xFF;
            ushort[] buf;
            try
            {
                
                switch (str)
                {
                    case "input":
                        buf = master.ReadInputRegisters(addr, reg_addr, num);
                        one_reg = buf[0];
                        if(need == true)
                            for (int i = 0;i<buf.Length; i++)
                                output += (addr).ToString("D3") + ":" + (reg_addr+i).ToString("D3") + " " + str + ": " + buf[i].ToString("D5") + "\n";
                        break;
                    case "holding":
                        buf = master.ReadHoldingRegisters(addr, reg_addr, num);
                        one_reg = buf[0];
                        if (need == true)
                            for (int i = 0; i < buf.Length; i++)
                                output += (addr).ToString("D3") + ":" + (reg_addr+i).ToString("D3") + " " + str + ": " + buf[i].ToString("D5") + "\n";
                        break;
                    default:
                        reg_addr = 31;
                        num = 1;
                        buf = master.ReadInputRegisters(addr, reg_addr, num);
                        one_reg = buf[0];
                        buf = master.ReadInputRegisters(addr, (ushort)(reg_addr + 1), (ushort)(one_reg * 8));
                        if (need == true)
                        {
                            for (int i = 0; i < buf.Length; i++)
                            {
                                if (i % 8 == 0) output += "\n";
                                if (i % 8 < 4) output += ((byte)buf[i]).ToString("X2") + ((byte)(buf[i] >> 8)).ToString("X2");
                                if (i % 8 == 4) output += " " + ((double)buf[i] / 10).ToString("F1") + " ";
                                if (i % 8 > 4) output += buf[i].ToString() + " ";
                            }
                            output += "\n";
                        }
                        break;
                }
            }
            catch
            {
                output += (addr).ToString("D3") + " " + str  + ":" + "Time out\n";
            }
            RaisePropertyChanged("Terminal");
            return one_reg;
        }

        public void Write_one_reg(byte addr, ushort reg_addr, ushort num, bool need = true)
        {
            try
            {
                master.WriteSingleRegister(addr, reg_addr, num);
                if (need == true)
                {
                    ushort[] buf = master.ReadHoldingRegisters(addr, reg_addr, 1);
                    for (int i = 0; i < buf.Length; i++)
                        output += (addr).ToString("D3") + ":" + (reg_addr).ToString("D3") + ": " + buf[i].ToString("X2");
                    output += "\n";
                }
            }
            catch
            {
                if (need == true) output += (addr).ToString("D3") + ":" + "Time out\n";
            }
            RaisePropertyChanged("Terminal");

        }

        public void Flash_state_mashine()
        {
            ushort shift = 0;
            bool need_to_ret_scan = false;
            DateTime time = DateTime.Now;
            TimeSpan timeSpan;
            byte[] to_crc = new byte[1024];
            while (state != State.wait)
                switch (state)
                {
                    case State.wait:
                        break;
                    case State.send_reset:
                        if (scan_state == "Scan")
                        {
                            Scan_switch(adress);
                            need_to_ret_scan = true;
                        }
                        Write_one_reg(flash_addr, 0, 384, false);
                        state = State.wait_boot_enter;
                        break;
                    case State.wait_boot_enter:
                        if (Read_reg(99, 0, 1, "holding", false) != 0xFF)
                        {
                            output += "Boot";
                            RaisePropertyChanged("Terminal");
                            state = State.sending_firm;
                        }
                        else state = State.wait;
                        break;
                    case State.sending_firm:
                        Crc32 crc32 = new Crc32();
                        uint crc;
                        while (shift * 0x400 < _File.Length)
                        {
                            Write_one_reg(99, 1, (UInt16)(0x2000 + shift * 0x400), false);
                            Write_one_reg(99, 2, 0x0800, false);
                            Write_one_reg(99, 3, (UInt16)(0x2000 + shift * 0x400 + 0x3FF), false);
                            Write_one_reg(99, 4, 0x0800, false);

                            for (int i = 0; i < 1024; i++)
                            {
                                if (i + shift * 0x400 < _File.Length) to_crc[i] = _File[i + shift * 0x400];
                                else to_crc[i] = 0xFF;
                            }
                            for (int i = 0; i < 8; i++)
                            {
                                try
                                {
                                    UInt16[] data = new UInt16[64];
                                    for (int k = 0; k < 64; ++k)
                                    {
                                        data[k] = Convert.ToUInt16((to_crc[(i * 128) + (k * 2 + 1)] << 8) + to_crc[(k * 2) + (i * 128)]);
                                    }
                                    master.WriteMultipleRegisters(99, (UInt16)(i * 64 + 7), data);
                                    RaisePropertyChanged("Terminal");
                                }
                                catch
                                {
                                    output += "flash:" + "Time out\n";
                                    RaisePropertyChanged("Terminal");
                                    break;
                                }
                            }
                            crc = crc32.GetCRC32B(to_crc);
                            Write_one_reg(99, 5, (UInt16)(crc), false);
                            Write_one_reg(99, 6, (UInt16)(crc >> 16), false);
                            Write_one_reg(99, 0, (UInt16)(512), false);
                            Read_reg(99, 11, 2, "input", false);
                            st_reg = Read_reg(99, 0, 1, "holding", false);
                            while (st_reg != 0x000)
                            {
                                if (st_reg == 0x800)
                                {
                                    output += "crc_mb_err\n";
                                    break;
                                }
                                if (st_reg == 0x1000)
                                {
                                    output += "crc_flash_err\n";
                                    break;
                                }
                                st_reg = Read_reg(99, 0, 1, "holding", false);
                            }
                            if(st_reg == 0x000) output +=", " + (shift*100/ (_File.Length / 1024)).ToString() + " %";
                            RaisePropertyChanged("Terminal");
                            shift++;
                        }


                        Write_one_reg(99, 1, (UInt16)(0x2000), false);
                        Write_one_reg(99, 2, 0x0800, false);
                        byte[] all_crc = new byte[shift * 0x400];
                        for (int i = 0; i < shift * 0x400; i++)
                        {
                            if (i < _File.Length) all_crc[i] = _File[i];
                            else all_crc[i] = 0xFF;
                        }
                        crc = crc32.GetCRC32B(all_crc);
                        Write_one_reg(99, 5, (UInt16)(crc), false);
                        Write_one_reg(99, 6, (UInt16)(crc >> 16), false);
                        Write_one_reg(99, 0, 1024, false);
                        st_reg = Read_reg(99, 0, 1, "holding", false);
                        while (st_reg != 0x000)
                        {
                            if (st_reg == 0x800)
                            {
                                output += "crc_mb_err\n";
                                break;
                            }
                            if (st_reg == 0x1000)
                            {
                                output += "crc_flash_err\n";
                                break;
                            }
                            st_reg = Read_reg(99, 0, 1, "holding", false);
                        }
                        Write_one_reg(99, 0, 256, false);
                        while (st_reg != 0x000)
                        {
                            if (st_reg == 0x800)
                            {
                                output += "crc_mb_err\n";
                                break;
                            }
                            if (st_reg == 0x1000)
                            {
                                output += "crc_flash_err\n";
                                break;
                            }
                            st_reg = Read_reg(99, 0, 1, "holding", false);
                        }
                        Write_one_reg(99, 0, 128, false);
                        timeSpan = DateTime.Now - time;
                        output += ", Flash done for " + timeSpan.ToString() + "\n";
                        if(need_to_ret_scan) Scan_switch(adress);
                        RaisePropertyChanged("Terminal");
                        state = State.wait;
                        break;
                    default:
                        state = State.wait;
                        break;
                } 
        }

        public void Console_dialog(string str)
        {
            string[]    parse;
            byte        addr;
            ushort      reg_addr;
            ushort      num;

            try
            {
                parse       = str.Split(' ');

                switch (parse.ElementAtOrDefault(0))
                {
                    case "baud":
                        baudrate = int.Parse(parse.ElementAtOrDefault(1));
                        output += "baudrate will be set at " + baudrate.ToString() + "\n";
                        break;
                    case "flash":
                        if (_File != null)
                        {
                            addr = Convert.ToByte(parse.ElementAtOrDefault(1));
                            reg_addr = Convert.ToUInt16(parse.ElementAtOrDefault(2));
                            num = Convert.ToUInt16(parse.ElementAtOrDefault(3));
                            state = State.send_reset;
                            flash_addr = addr;
                            Thread newThread = new Thread(Flash_state_mashine);
                            newThread.Start();
                        }
                        else output += "empty bin, open firmware\n";
                        break;
                    case "read":
                        addr = Convert.ToByte(parse.ElementAtOrDefault(1));
                        reg_addr = Convert.ToUInt16(parse.ElementAtOrDefault(2));
                        num = Convert.ToUInt16(parse.ElementAtOrDefault(3));
                        Read_reg(addr, reg_addr, num, parse.ElementAtOrDefault(4));
                        break;
                    case "restart":
                        addr = Convert.ToByte(parse.ElementAtOrDefault(1));
                        reg_addr = Convert.ToUInt16(parse.ElementAtOrDefault(2));
                        num = Convert.ToUInt16(parse.ElementAtOrDefault(3));
                        Write_one_reg(addr, 0, 128);
                        break;
                    case "write":
                        addr = Convert.ToByte(parse.ElementAtOrDefault(1));
                        reg_addr = Convert.ToUInt16(parse.ElementAtOrDefault(2));
                        num = Convert.ToUInt16(parse.ElementAtOrDefault(3));
                        Write_one_reg(addr, reg_addr, num);
                        break;
                    case "test":
                        if (parse.ElementAtOrDefault(1) != null) addr = Convert.ToByte(parse.ElementAtOrDefault(1));
                        else addr = 1;
                        Read_reg(addr, 0, 1, "input");
                        break;
                    case "help":
                        output +=   "read       [adress] [register] [numbers of register] [type: input/holding]\n" +
                                    "write      [adress] [register] [value] \n" +
                                    "flash      [adress] \n" +
                                    "restart    [adress] \n" +
                                    "baud       [baudrate] \n";
                        break;
                    default:
                        output += "Wrong command\n";
                        break;
                }
            }
            catch(Exception e)
            {
                output += "Wrong command format\nType help to list all comand \n";
                output += e + "\n";

            }
            RaisePropertyChanged("Terminal");
        }

        public void Open_Firmware()
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {
                InitialDirectory = "d:\\",
                Filter = "hex (*.hex)|*.hex|bin (*.bin)|*.bin",
                FilterIndex = 2,
                RestoreDirectory = true
            };
            openFileDialog1.ShowDialog();
            try
            {
                if (openFileDialog1.OpenFile() != null)
                {
                    _File = File.ReadAllBytes(openFileDialog1.FileName);
                    output += "File opened \n";                  
                }
            }
            catch (Exception ex)
            {
                output +=("Error: Could not read file from disk. Original error: " + ex.Message + "\n");
            }
            RaisePropertyChanged("Terminal");
        }

        public void Scan_switch(string adr)
        {
            if (scan_state == "Wait")   scan_state = "Scan";
            else                        scan_state = "Wait";
            adress = adr;
            RaisePropertyChanged("Scan_state");
        }

        public void TimerElapsedEventHandler(object sender, EventArgs args)
        {
            if (port.IsOpen && scan_state == "Scan")
            {
                var tmp = Read_reg(Convert.ToByte(adress), 8, 1, "input", false);
                if ((tmp & 0xF000) == 0x1000)
                {
                    ushort ht = Read_reg(Convert.ToByte(adress), 7, 1, "input", false);
                    ushort temp = Read_reg(Convert.ToByte(adress), 8, 1, "input", false);
                    output += "Влажность: " + ((Double)ht / 10).ToString("F1") + "% " + "Температура: " + ((Double)temp / 10).ToString("F1") + " c\n";
                }
                else if ((tmp & 0xF000) == 0x2000)
                {
                    ushort crc = Read_reg(Convert.ToByte(adress), 8, 1, "input", false);
                    if (crc != 0)
                    {
                        Write_one_reg(Convert.ToByte(adress), 0, 0x0011, false);
                        output += "crc error cnt:" + crc.ToString("D") + "\n";
                    }
                    Int32 val = (Int16)Read_reg(Convert.ToByte(adress), 7, 1, "input", false);
                    val = val * 10000 / 8192;
                    output += "Индукция = " + ((Double)val / 1000).ToString("F3") + " мТл \n";
                }
                else if ((tmp & 0xF000) == 0x3000)
                {
                    var t = Read_reg(Convert.ToByte(adress), 31, 1, "input", false);
                    for(var i = 0; i < t; i++)
                    {
                        var val_1 = Read_reg(Convert.ToByte(adress), (ushort)(10 + i), 1, "input", false);
                        var val_2 = Read_reg(Convert.ToByte(adress), (ushort)(10 + i+4), 1, "input", false);
                        output += "Ch " + i.ToString() + " Raw: " + val_1.ToString("D5") + " Conv: " + ((Double)val_2 / 1000).ToString("F3") + " v \n";
                    }
                    ushort ht = Read_reg(Convert.ToByte(adress), 18, 1, "input", false);
                    ushort temp = Read_reg(Convert.ToByte(adress), 19, 1, "input", false);
                    output += "Влажность: " + ((Double)ht / 10).ToString("F1") + "% " + "Температура: " + ((Double)temp / 10).ToString("F1") + " c\n";
                    output += "\n";
                }
                else
                {
                    Read_reg(Convert.ToByte(adress), 0, 0, null);
                }
            }
        }

    }
}


