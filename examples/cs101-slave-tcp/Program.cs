﻿using System;
using System.IO.Ports;
using lib60870;
using lib60870.CS101;
using System.Collections.Generic;
using lib60870.linklayer;
using System.Threading;

namespace cs101_slave_tcp
{
    public class CS101TestSlave 
    {

        private static bool myInterrogationHandler(object parameter, IMasterConnection connection, ASDU asdu, byte qoi)
        {
            Console.WriteLine("Interrogation for group " + qoi);

            connection.SendACT_CON (asdu, false);

            // send information objects
            ASDU newAsdu = new ASDU(connection.GetApplicationLayerParameters(), CauseOfTransmission.INTERROGATED_BY_STATION, 
                false, false, 2, 1, false);

            newAsdu.AddInformationObject (new MeasuredValueScaled (100, -1, new QualityDescriptor ()));

            newAsdu.AddInformationObject (new MeasuredValueScaled (101, 23, new QualityDescriptor ()));

            newAsdu.AddInformationObject (new MeasuredValueScaled (102, 2300, new QualityDescriptor ()));

            connection.SendASDU (newAsdu);

            // send sequence of information objects
            newAsdu = new ASDU (connection.GetApplicationLayerParameters(), CauseOfTransmission.INTERROGATED_BY_STATION, 
                false, false, 2, 1, true);

            newAsdu.AddInformationObject (new SinglePointInformation (200, true, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (201, false, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (202, true, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (203, false, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (204, true, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (205, false, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (206, true, new QualityDescriptor ()));
            newAsdu.AddInformationObject (new SinglePointInformation (207, false, new QualityDescriptor ()));

            connection.SendASDU (newAsdu);

            connection.SendACT_TERM (asdu);

            return true;
        }

        public static void Main (string[] args)
        {
            bool running = true;

            // use Ctrl-C to stop the programm
            Console.CancelKeyPress += delegate(object? sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                running = false;
            };
                
            LinkLayerParameters llParameters = new LinkLayerParameters ();
            llParameters.AddressLength = 1;
            llParameters.TimeoutForACK = 500;
            llParameters.UseSingleCharACK = true;

            TcpServerVirtualSerialPort port = new TcpServerVirtualSerialPort ();
            //TcpClientVirtualSerialPort port = new TcpClientVirtualSerialPort("192.168.2.9", 2404);

            port.DebugOutput = true;
            port.Start ();

            CS101Slave slave = new CS101Slave (port, llParameters);
            slave.DebugOutput = true;
            slave.LinkLayerAddress = 3;
            slave.LinkLayerAddressOtherStation = 1;

            slave.LinkLayerMode = lib60870.linklayer.LinkLayerMode.BALANCED;

            slave.SetInterrogationHandler (myInterrogationHandler, null);

            slave.SetUserDataQueueSizes (50, 20);

            ASDU asdu = new ASDU (slave.Parameters, CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);
            asdu.AddInformationObject (new StepPositionInformation (301, 1, false, new QualityDescriptor()));
            slave.EnqueueUserDataClass1 (asdu);

            long lastTimestamp = SystemUtils.currentTimeMillis ();
            Int16 measuredValue = 0;

            TransparentFile file = new TransparentFile (1, 30000, NameOfFile.TRANSPARENT_FILE);

            byte[] fileData = new byte[1025];

            for (int i = 0; i < 1025; i++)
                fileData [i] = (byte)(i + 1);

            file.AddSection (fileData);

            slave.GetAvailableFiles().AddFile (file);

            while (running) {

                slave.Run (); // call the protocol stack

                if ((SystemUtils.currentTimeMillis() - lastTimestamp) >= 5000) {

                    lastTimestamp = SystemUtils.currentTimeMillis ();

                    ASDU newAsdu = new ASDU (slave.Parameters, CauseOfTransmission.PERIODIC, false, false, 0, 1, false);
                    newAsdu.AddInformationObject (new MeasuredValueScaled (110, measuredValue, new QualityDescriptor ()));
                    slave.EnqueueUserDataClass2 (newAsdu);

                    measuredValue++;
                }

                if (Console.KeyAvailable) {

                    ConsoleKeyInfo keyInfo = Console.ReadKey ();

                    if (keyInfo.KeyChar == 't') {
                        slave.SendLinkLayerTestFunction ();
                    } 
                    else {
                        Console.WriteLine ("Send spontaneous message");

                        bool value = false;

                        if (keyInfo.KeyChar == 's') {
                            value = true;
                        }

                        ASDU newAsdu = new ASDU (slave.Parameters, CauseOfTransmission.SPONTANEOUS, false, false, 0, 1, false);
                        newAsdu.AddInformationObject (new SinglePointInformation (100, value, new QualityDescriptor ()));

                        slave.EnqueueUserDataClass1 (newAsdu);
                    }
                }

                Thread.Sleep(1);
            }

            port.Stop ();
        }
    }
}
