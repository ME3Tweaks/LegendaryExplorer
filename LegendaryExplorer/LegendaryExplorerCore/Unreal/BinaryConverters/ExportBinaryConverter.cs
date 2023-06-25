using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Classes;
using static LegendaryExplorerCore.Unreal.BinaryConverters.ObjectBinary;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public static class ExportBinaryConverter
    {
        public static ObjectBinary ConvertPostPropBinary(ExportEntry export, MEGame newGame, PropertyCollection newProps)
        {
            if (export.propsEnd() == export.DataSize)
            {
                return Array.Empty<byte>();
            }

            if (export.IsTexture())
            {
                return ConvertTexture2D(export, newGame);
            }

            var from = From(export);
            if (from is ObjectBinary objbin)
            {
                if (objbin is AnimSequence animSeq)
                {
                    animSeq.UpdateProps(newProps, newGame);
                }


                // IDK if this works as internal busses have changed identifiers.
                // You likely would need to correct bus IDs internally for this to properly work.
                // Todo: LE3 -> LE2
                else if (objbin is WwiseEvent we && export.Game is MEGame.LE2 or MEGame.LE3 && newGame is /*MEGame.LE2 or*/ MEGame.LE3 && export.Game != newGame)
                {
                    // We can't convert ME2 -> ME3, only LE versions work

                    // LE2: Properties
                    // LE3: Binary

                    if (export.Game == MEGame.LE2)
                    {
                        var refs = export.GetProperty<ArrayProperty<StructProperty>>(@"References");
                        if (refs != null && refs.Count == 1)
                        {
                            var relationships = refs[0].GetProp<StructProperty>(@"Relationships");
                            var streams = relationships.GetProp<ArrayProperty<ObjectProperty>>(@"Streams");
                            relationships.Properties.Remove(streams); // Remove the property, does not exist in LE3.

                            we.Links = new List<WwiseEvent.WwiseEventLink>();
                            we.Links.Add(new WwiseEvent.WwiseEventLink() { WwiseStreams = streams.Properties.Select(x => x.Value).ToList() });
                            newProps.Add(relationships);
                        }
                    }

                    //if (export.Game == MEGame.LE3)
                    //{
                    //    // LE3
                    //    newProps.Add(new StructProperty("WwiseRelationships", false,
                    //        new ObjectProperty(bankExport, "Bank"))
                    //    { Name = "Relationships" });
                    //    p.Add(new IntProperty((int)eventInfo.Id, "Id"));


                    //    p.Add(new FloatProperty(9, "Duration")); // TODO: FIGURE THIS OUT!!! THIS IS A PLACEHOLDER

                    //    // Todo: Write the WwiseStreams
                    //}
                    //else
                    //{
                    //    // LE2

                    //    var references = new ArrayProperty<StructProperty>("References");
                    //    var platProps = new PropertyCollection();

                    //    var platSpecificProps = new PropertyCollection();
                    //    platSpecificProps.Add(new ArrayProperty<ObjectProperty>(streamExports.Select(x => new ObjectProperty(x.UIndex)), "Streams"));
                    //    platSpecificProps.Add(new ObjectProperty(bankExport, "Bank"));
                    //    platProps.Add(new StructProperty("WwiseRelationships", platSpecificProps, "Relationships"));
                    //    platProps.Add(new IntProperty(1, "Platform"));
                    //    var platRef = new StructProperty("WwisePlatformRelationships", platProps);
                    //    references.Add(platRef);
                    //    p.Add(references);
                    //}

                    //WwiseEvent we = new WwiseEvent();
                    //we.WwiseEventID = eventInfo.Id;
                    //we.Links = new List<WwiseEvent.WwiseEventLink>();

                    //// LE3 puts this in binary instead of properties
                    //if (package.Game == MEGame.LE3)
                    //{
                    //    we.Links.Add(new WwiseEvent.WwiseEventLink()
                    //    { WwiseStreams = streamExports.Select(x => x.UIndex).ToList() });
                    //}
                    //else
                    //{
                    //    // LE2
                    //    we.WwiseEventID = eventInfo.Id; // ID is stored here
                    //}



                    //}
                }
                return objbin;
            }




            switch (export.ClassName)
            {
                case "DirectionalLightComponent":
                case "PointLightComponent":
                case "SkyLightComponent":
                case "SphericalHarmonicLightComponent":
                case "SpotLightComponent":
                case "DominantSpotLightComponent":
                case "DominantPointLightComponent":
                case "DominantDirectionalLightComponent":
                    if (newGame == MEGame.UDK)
                    {
                        return Array.Empty<byte>();
                    }
                    else if (export.Game == MEGame.UDK)
                    {
                        return new byte[8];
                    }
                    break;
            }

            //no conversion neccesary
            return export.GetBinaryData();
        }

        public static byte[] ConvertPrePropBinary(ExportEntry export, MEGame newGame)
        {
            if (export.HasStack)
            {
                var ms = new MemoryStream(export.Data);
                int node = ms.ReadInt32();
                int stateNode = 0;
                if (export.Game != MEGame.UDK)
                {
                    stateNode = ms.ReadInt32();
                }
                ulong probeMask = ms.ReadUInt64();
                if (export.Game >= MEGame.ME3)
                {
                    ms.SkipInt16();
                }
                else
                {
                    ms.SkipInt32();
                }

                int count = ms.ReadInt32();
                byte[] stateStack = ms.ReadToBuffer(count * 12);
                int offset = 0;
                if (node != 0)
                {
                    offset = ms.ReadInt32();
                }

                int netIndex = ms.ReadInt32();

                using var os = MemoryManager.GetMemoryStream();
                os.WriteInt32(node);
                if (newGame != MEGame.UDK)
                {
                    os.WriteInt32(stateNode);
                }
                os.WriteUInt64(probeMask);
                if (newGame >= MEGame.ME3)
                {
                    os.WriteUInt16(0);
                }
                else
                {
                    os.WriteUInt32(0);
                }
                os.WriteInt32(count);
                os.WriteFromBuffer(stateStack);
                if (node != 0)
                {
                    os.WriteInt32(offset);
                }
                os.WriteInt32(netIndex);

                return os.ToArray();
            }

            switch (export.ClassName)
            {
                case "DominantSpotLightComponent":
                case "DominantDirectionalLightComponent":
                    //todo: do conversion for these
                    break;
            }

            return export.DataReadOnly.Slice(0, export.GetPropertyStart()).ToArray();
        }

        public static byte[] ConvertTexture2D(ExportEntry export, MEGame newGame, List<int> offsets = null, StorageTypes newStorageType = StorageTypes.empty)
        {
            MemoryStream bin = export.GetReadOnlyBinaryStream();
            if (bin.Length == 0)
            {
                return bin.ToArray();
            }
            using var os = MemoryManager.GetMemoryStream();

            if (!export.Game.IsGame3())
            {
                bin.Skip(8);
                bin.Skip(bin.ReadInt32()); // Skip the thumbnail
                bin.Skip(4); // Skip fileOffset
            }
            if (!newGame.IsGame3())
            {
                os.WriteZeros(16);//includes fileOffset, but that will be set during save
            }

            int mipCount = bin.ReadInt32();
            long mipCountPosition = os.Position;
            os.WriteInt32(mipCount);
            List<Texture2DMipInfo> mips = Texture2D.GetTexture2DMipInfos(export, export.GetProperty<NameProperty>("TextureFileCacheName")?.Value);
            int offsetIdx = 0;
            int trueMipCount = 0;
            for (int i = 0; i < mipCount; i++)
            {
                var storageType = (StorageTypes)bin.ReadInt32(); // The existing storage type
                int uncompressedSize = bin.ReadInt32();
                int compressedSize = bin.ReadInt32();
                int fileOffset = bin.ReadInt32();
                byte[] texture;
                switch (storageType)
                {
                    case StorageTypes.pccUnc:
                        texture = bin.ReadToBuffer(uncompressedSize);
                        break;
                    case StorageTypes.pccLZO:
                    case StorageTypes.pccZlib:
                    case StorageTypes.pccOodle:
                        texture = bin.ReadToBuffer(compressedSize);

                        // Todo: This needs to be handled in the caller if it's handled there
                        texture = TextureCompression.ConvertTextureCompression(texture, uncompressedSize, ref storageType, newGame, true);
                        compressedSize = texture.Length;
                        if (offsets != null)
                        {
                            //the caller has transferred this mip, so use the provided data
                            texture = Array.Empty<byte>();
                            fileOffset = offsets[offsetIdx++];
                            compressedSize = offsets[offsetIdx] - fileOffset;
                            if (newStorageType != StorageTypes.empty)
                            {
                                storageType = newStorageType;
                            }
                        }
                        break;
                    case StorageTypes.empty:
                        texture = Array.Empty<byte>();
                        break;
                    default:
                        if (export.Game != newGame)
                        {
                            storageType &= (StorageTypes)~StorageFlags.externalFile;
                            texture = Texture2D.GetTextureData(mips[i], export.Game, export.Game != MEGame.UDK ? MEDirectories.GetDefaultGamePath(export.Game) : null, false); //copy in external textures
                            if (storageType != StorageTypes.pccUnc)
                            {
                                texture = TextureCompression.ConvertTextureCompression(texture, uncompressedSize, ref storageType, newGame, true); // Convert the storage type to work with the listed game
                                compressedSize = texture.Length;
                            }
                        }
                        else
                        {
                            texture = Array.Empty<byte>();
                        }
                        break;

                }

                int width = bin.ReadInt32();
                int height = bin.ReadInt32();
                if (newGame == MEGame.UDK && storageType == StorageTypes.empty)
                {
                    continue;
                }
                trueMipCount++;
                os.WriteInt32((int)storageType);
                os.WriteInt32(uncompressedSize);
                os.WriteInt32(compressedSize);
                os.WriteInt32(fileOffset);//fileOffset will be fixed during save
                os.WriteFromBuffer(texture);
                os.WriteInt32(width);
                os.WriteInt32(height);

            }

            long postMipPosition = os.Position;
            os.JumpTo(mipCountPosition);
            os.WriteInt32(trueMipCount);
            os.JumpTo(postMipPosition);

            int unk1 = 0;
            if (export.Game != MEGame.UDK)
            {
                unk1 = bin.ReadInt32();
            }
            if (newGame != MEGame.UDK)
            {
                os.WriteInt32(unk1);
            }

            Guid textureGuid = export.Game != MEGame.ME1 ? bin.ReadGuid() : Guid.NewGuid();

            if (newGame != MEGame.ME1)
            {
                os.WriteGuid(textureGuid);
            }

            if (export.Game == MEGame.UDK)
            {
                bin.Skip(32);
            }
            if (newGame == MEGame.UDK)
            {
                os.WriteZeros(4 * 8);
            }

            if (export.Game == MEGame.ME3 || export.Game.IsLEGame())
            {
                bin.Skip(4);
            }
            if (newGame == MEGame.ME3 || newGame.IsLEGame())
            {
                os.WriteInt32(0);
            }
            if (export.ClassName == "LightMapTexture2D")
            {
                int lightMapFlags = 0;
                if (export.Game >= MEGame.ME3)
                {
                    lightMapFlags = bin.ReadInt32();
                }
                if (newGame >= MEGame.ME3)
                {
                    os.WriteInt32(0);//LightMapFlags noflag
                }
            }

            return os.ToArray();
        }
    }

}
