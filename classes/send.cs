                                        int regcount = (Convert.ToInt32(cmd_params[3]) / 2);
                                        for (int i = 0; i < regcount / 64; ++i)
                                        {
                                            reg.RegisterAddress = Convert.ToUInt16(7 + (i * 64));
                                            try
                                            {
                                                UInt16[] data = new UInt16[64];
                                                for (int k = 0; k < 64; ++k)
                                                {
                                                    data[k] = Convert.ToUInt16((page_to_send[(i * 128) + (k * 2 + 1)] << 8) + page_to_send[(k * 2) + (i * 128)]);
                                                }
                                                SetModbusData(reg, data);                                                
                                                NetSendMessage("status:" + (i*64).ToString() + ":" + regcount.ToString());
                                            }
                                            catch (Exception ex)
                                            {
                                                WriteLog("errorpage: " + ex.Message);
                                                //WriteLog("errorpage: " + ex.Message);
                                            }
                                        }