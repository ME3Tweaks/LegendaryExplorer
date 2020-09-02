//using System.Collections.Generic;
//using System.IO;
//using ME3ExplorerCore.Helpers;

//namespace ME3Explorer.Soundplorer
//{
//    /// <summary>
//    /// Based on https://github.com/bgbennyboy/Psychonauts-Explorer/blob/master/uXboxAdpcmDecoder.pas
//    /// </summary>
//    class XboxADPCMDecoder
//    {
//        readonly int[] StepTable = { 0x7, 0x8, 0x9, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x10, 0x11, 0x13, 0x15, 0x17, 0x19, 0x1C,
//    0x1F, 0x22, 0x25, 0x29, 0x2D, 0x32, 0x37, 0x3C, 0x42, 0x49, 0x50, 0x58, 0x61, 0x6B,
//    0x76, 0x82, 0x8F, 0x9D, 0x0AD, 0x0BE, 0x0D1, 0x0E6, 0x0FD, 0x117, 0x133, 0x151,
//    0x173, 0x198, 0x1C1, 0x1EE, 0x220, 0x256, 0x292, 0x2D4, 0x31C, 0x36C, 0x3C3,
//    0x424, 0x48E, 0x502, 0x583, 0x610, 0x6AB, 0x756, 0x812, 0x8E0, 0x9C3, 0x0ABD,
//    0x0BD0, 0x0CFF, 0x0E4C, 0x0FBA, 0x114C, 0x1307, 0x14EE, 0x1706, 0x1954, 0x1BDC,
//    0x1EA5, 0x21B6, 0x2515, 0x28CA, 0x2CDF, 0x315B, 0x364B, 0x3BB9, 0x41B2, 0x4844,
//    0x4F7E, 0x5771, 0x602F, 0x69CE, 0x7462, 0x7FFF};

//        readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8, -1, -1, -1, -1, 2, 4, 6, 8 };
//        private int FBlockSize;
//        private uint FChannels;

//        private class ADPCMState
//        {
//            public ushort Predictor;
//            public int Index;
//            public int StepSize;
//        }

//        public XboxADPCMDecoder(uint AChannels)
//        {
//            switch (AChannels)
//            {
//                case 0:
//                    FBlockSize = 0x20;
//                    break;
//                case 1:
//                    FBlockSize = 0x40;
//                    break;
//            }
//            FChannels = AChannels;
//        }

//        public MemoryStream Decode(Stream inStream, int sourcePos, int sourceSize)
//        {
//            MemoryStream decodedStream = new MemoryStream();
//            uint codeBuf;
//            short[,] Buffers = new short[2, 8];
//            List<ADPCMState> adpcmStates = new List<ADPCMState>();
//            adpcmStates.Add(new ADPCMState()); //channel 1
//            adpcmStates.Add(new ADPCMState()); //channel 2

//            while (inStream.Position < sourcePos + sourceSize)
//            {
//                // read the adpcm header
//                PrepareAdpcmState(adpcmStates[0], inStream, decodedStream);
//                if (FChannels == 2) PrepareAdpcmState(adpcmStates[1], inStream, decodedStream);
//                for (int i = 0; i < 8; i++)
//                {
//                    int channel = 0;
//                    codeBuf = inStream.ReadUInt32();
//                    for (int bufferIndex = 0; bufferIndex < 8; bufferIndex++)
//                    {
//                        Buffers[channel, bufferIndex] = (short)DecodeSample(codeBuf & 0xF, adpcmStates[channel]);
//                        codeBuf = codeBuf >> 4;
//                    }
//                    if (FChannels == 2)
//                    {
//                        //Read Channel 2
//                        channel = 1;
//                        codeBuf = inStream.ReadUInt32();
//                        for (int bufferIndex = 0; bufferIndex < 8; bufferIndex++)
//                        {
//                            Buffers[channel, bufferIndex] = (short)DecodeSample(codeBuf & 0xF, adpcmStates[channel]);
//                            codeBuf = codeBuf >> 4;
//                        }
//                    }
//                    //Write the decoded samples
//                    for (int bufferIndex = 0; bufferIndex < 8; bufferIndex++)
//                    {
//                        decodedStream.WriteInt16(Buffers[0, bufferIndex]);
//                        if (FChannels == 2)
//                        {
//                            decodedStream.WriteInt16(Buffers[1, bufferIndex]);
//                        }
//                    }
//                }
//            }

//            return decodedStream;
//        }

//        private int DecodeSample(uint Code, ADPCMState State)
//        {
//            int result = 0;
//            int delta = 0;
//            if ((Code & 0x4) == 4) delta += State.StepSize;
//            if ((Code & 0x2) == 2) delta += State.StepSize >> 1;
//            if ((Code & 0x1) == 1) delta += State.StepSize >> 2;
//            delta += State.StepSize >> 3;
//            //Check for sign bit and flip result
//            if ((Code & 0x8) == 8) { delta = -delta; }
//            result = State.Predictor + delta;

//            //clip the sample
//            if (result > short.MaxValue)
//            {
//                result = short.MaxValue;
//            }
//            else if (result < short.MinValue)
//            {
//                result = short.MinValue;
//            }
//            State.Index += IndexTable[Code];
//            //clip the index
//            if (State.Index < 0)
//            {
//                State.Index = 0;
//            }
//            else if (State.Index > 88)
//            {
//                State.Index = 88;
//            }

//            State.StepSize = StepTable[State.Index];
//            State.Predictor = (ushort)result;
//            return result;
//        }

//        private void PrepareAdpcmState(ADPCMState pc, Stream inStream, MemoryStream decodedStream)
//        {
//            pc.Predictor = inStream.ReadUInt16();
//            pc.Index = inStream.ReadUInt8();
//            inStream.ReadUInt8(); //advance by 1
//            //if (pc.Index > StepTable.Count() - 1) pc.Index = StepTable.Count() - 1;
//            pc.StepSize = StepTable[pc.Index];
//            decodedStream.WriteUInt16(pc.Predictor);
//        }
//    }
//}
