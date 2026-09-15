using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace EmergencyVR.Desktop
{
    public static class RuntimeCapture
    {
        public static void Save(Camera camera,string path)
        {
            var target=new RenderTexture(1440,1000,24);var pixels=new Texture2D(1440,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try {target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1440,1000),0,0);pixels.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,EncodePng(pixels));}
            finally {RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
        // Runtime ImageConversion is not enabled in this project. PNG encoding uses existing .NET only.
        static byte[] EncodePng(Texture2D texture)
        {
            int width=texture.width,height=texture.height;var colors=texture.GetPixels32();
            var raw=new byte[height*(width*3+1)];int k=0;
            for(int y=height-1;y>=0;y--){raw[k++]=0;for(int x=0;x<width;x++){var c=colors[y*width+x];raw[k++]=c.r;raw[k++]=c.g;raw[k++]=c.b;}}
            using(var output=new MemoryStream())
            {
                output.Write(new byte[]{137,80,78,71,13,10,26,10},0,8);
                using(var header=new MemoryStream()){UInt(header,(uint)width);UInt(header,(uint)height);header.Write(new byte[]{8,2,0,0,0},0,5);Chunk(output,"IHDR",header.ToArray());}
                using(var compressed=new MemoryStream())
                {
                    compressed.WriteByte(0x78);compressed.WriteByte(0x9c);
                    using(var deflate=new DeflateStream(compressed,System.IO.Compression.CompressionLevel.Optimal,true))deflate.Write(raw,0,raw.Length);
                    uint a=1,b=0;foreach(byte value in raw){a=(a+value)%65521;b=(b+a)%65521;}UInt(compressed,(b<<16)|a);
                    Chunk(output,"IDAT",compressed.ToArray());
                }
                Chunk(output,"IEND",System.Array.Empty<byte>());return output.ToArray();
            }
        }
        static void UInt(Stream stream,uint value){stream.WriteByte((byte)(value>>24));stream.WriteByte((byte)(value>>16));stream.WriteByte((byte)(value>>8));stream.WriteByte((byte)value);}
        static void Chunk(Stream stream,string name,byte[] data)
        {
            UInt(stream,(uint)data.Length);var type=Encoding.ASCII.GetBytes(name);stream.Write(type,0,4);stream.Write(data,0,data.Length);
            uint crc=0xffffffff;foreach(byte value in type)Crc(value);foreach(byte value in data)Crc(value);UInt(stream,crc^0xffffffff);
            void Crc(byte value){crc^=value;for(int i=0;i<8;i++)crc=(crc&1)!=0?0xedb88320^(crc>>1):crc>>1;}
        }
    }
}
