﻿/*
Copyright VBChunguk  2012-2013

Permission is hereby granted, free of charge, to any person
obtaining a copy of this software and associated documentation
files (the "Software"), to deal in the Software without
restriction, including without limitation the rights to use,
copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the
Software is furnished to do so, subject to the following
conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Text;

namespace th105Edit
{
    public enum cvnType
    {
        Text,
        CSV,
        Graphic,
        Audio,
        Unknown
    }

    public class cv1DataLine
    {
        private string[] fields;
        public string[] Fields
        {
            get { return fields; }
        }

        public cv1DataLine()
        {
            fields = null;
        }
        public cv1DataLine(string Data)
            : this()
        {
            fields = Data.Split(new char[] { ',' });
        }

        public override string ToString()
        {
            if (fields == null) return string.Empty;
            string result = string.Empty;
            bool first = true;
            foreach (string i in fields)
            {
                if (!first) result += ",";
                first = false;
                if (i.IndexOf(',') != -1)
                    result += "\"" + i + "\"";
                else
                    result += i;
            }
            return result;
        }
    }
    public class cv1DataCollection : Collection<cv1DataLine>
    {
        public override string ToString()
        {
            string result = string.Empty;
            foreach (cv1DataLine i in this)
            {
                result += i.ToString() + "\r\n";
            }
            return result;
        }
    }

    public class cv3Stream : Stream
    {
        private long m_virtual_position, m_real_position;
        private Stream m_internal;
        private byte[] m_header;

        public cv3Stream()
            : base()
        {
            m_virtual_position = m_real_position = 0;
            m_internal = null;
            m_header = new byte[0x2c];
        }
        public cv3Stream(Stream fp)
            : this()
        {
            m_internal = fp;
            Array.Copy(Encoding.ASCII.GetBytes("RIFF"), 0, m_header, 0x00, 4);
            Array.Copy(LittleEndian.ToEndian((int)Length), 0, m_header, 0x04, 4);
            Array.Copy(Encoding.ASCII.GetBytes("WAVEfmt "), 0, m_header, 0x08, 8);
            Array.Copy(new byte[4] { 0x10, 0, 0, 0 }, 0, m_header, 0x10, 4);
            byte[] buffer = new byte[0x10];
            m_internal.Seek(0, SeekOrigin.Begin);
            m_internal.Read(buffer, 0, 0x10);
            Array.Copy(buffer, 0, m_header, 0x14, 0x10);
            Array.Copy(Encoding.ASCII.GetBytes("data"), 0, m_header, 0x24, 4);
            Array.Copy(LittleEndian.ToEndian((int)Length - 0x2c), 0, m_header, 0x28, 4);
        }

        public override bool CanRead
        {
            get { return true; }
        }
        public override bool CanSeek
        {
            get { return true; }
        }
        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            return;
        }

        public override long Length
        {
            get { return m_internal.Length + 0x1c; }
        }

        public override long Position
        {
            get
            {
                return m_virtual_position;
            }
            set
            {
                Seek(value, SeekOrigin.Begin);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            byte[] real_buffer = new byte[count];
            int virtual_left = 0x2c - (int)m_virtual_position;
            if (virtual_left < 0) virtual_left = 0;
            if (virtual_left > count) virtual_left = count;
            if (virtual_left > 0) Array.Copy(m_header, m_virtual_position, real_buffer, 0, virtual_left);
            Seek(virtual_left, SeekOrigin.Current);
            count -= virtual_left;
            if (count <= 0) return virtual_left;

            int read = m_internal.Read(real_buffer, virtual_left, count);
            Array.Copy(real_buffer, 0, buffer, offset, count + virtual_left);

            return virtual_left + read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: m_virtual_position = offset; break;
                case SeekOrigin.Current: m_virtual_position += offset; break;
                case SeekOrigin.End: m_virtual_position = Length + offset; break;
            }
            if (m_virtual_position < 0) m_virtual_position = 0;
            if (m_virtual_position > Length) m_virtual_position = Length;

            if (m_virtual_position < 0x14)
                m_real_position = 0;
            else if (m_virtual_position < 0x24)
                m_real_position = m_virtual_position - 0x14;
            else if (m_virtual_position < 0x2c)
                m_real_position = 0x10;
            else
                m_real_position = m_virtual_position - 0x1c;
            m_internal.Seek(m_real_position, SeekOrigin.Begin);
            return m_virtual_position;
        }

        public override void SetLength(long value)
        {
            return;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            return;
        }
    }

    public abstract class cvnBase : IDisposable
    {
        protected cvnType m_type;
        public cvnType Type
        {
            get { return m_type; }
        }

        protected string m_path;
        public string Path
        {
            get { return m_path; }
        }

        public abstract object Data
        {
            get;
        }
        public abstract void SetData(object Data);

        protected cvnBase()
        {
        }

        public virtual void Open(string Path)
        {
            FileStream fstr = new FileStream(Path, FileMode.Open);
            Open(fstr);
        }
        public abstract void Open(Stream fp);
        public abstract void SaveToFile(string Path);
        public abstract Stream ToStream();
        public abstract void Extract(string Path);

        public abstract void Dispose();

        public override string ToString()
        {
            return "cvn_helper.cvnBase";
        }
    }
    public class cv0 : cvnBase
    {
        public cv0()
            : base()
        {
            m_type = cvnType.Text;
            m_path = string.Empty;
            m_encoding = null;
            m_buf = null;
        }
        public cv0(string Path)
            : this()
        {
            Open(Path);
        }
        public cv0(Stream fp)
            : this()
        {
            Open(fp);
        }

        private byte[] m_buf;
        private Encoding m_encoding;
        public virtual Encoding StringEncoding
        {
            get { return m_encoding; }
            set { m_encoding = value; }
        }

        public override void Open(string Path)
        {
            Open(Path, Encoding.GetEncoding("Shift-JIS"));
        }
        public virtual void Open(string Path, Encoding StringEncoding)
        {
            FileStream fp = new FileStream(Path, FileMode.Open);
            Open(fp, StringEncoding);
        }
        public override void Open(Stream fp)
        {
            Open(fp, Encoding.GetEncoding("Shift-JIS"));
        }
        public virtual void Open(Stream fp, Encoding StringEncoding)
        {
            fp.Seek(0, SeekOrigin.Begin);
            long len = fp.Length;
            m_buf = new byte[len];
            fp.Read(m_buf, 0, (int)len);
            fp.Close();

            byte key, delta;
            const byte d_delta = 0x6b;
            key = 0x8b;
            delta = 0x71;
            for (long i = 0; i < len; i++)
            {
                m_buf[i] ^= key;
                key += delta;
                delta -= d_delta;
            }
            m_encoding = StringEncoding;
        }

        public override void SaveToFile(string Path)
        {
            int len = m_buf.Length;
            byte[] m_newbuf = new byte[m_buf.Length];
            m_buf.CopyTo(m_newbuf, 0);
            byte key, delta;
            const byte d_delta = 0x6b;
            key = 0x8b;
            delta = 0x71;
            for (int i = 0; i < len; i++)
            {
                m_newbuf[i] ^= key;
                key += delta;
                delta -= d_delta;
            }
            File.WriteAllBytes(Path, m_newbuf);
        }
        public override Stream ToStream()
        {
            int len = m_buf.Length;
            byte[] m_newbuf = new byte[m_buf.Length];
            m_buf.CopyTo(m_newbuf, 0);
            byte key, delta;
            const byte d_delta = 0x6b;
            key = 0x8b;
            delta = 0x71;
            for (int i = 0; i < len; i++)
            {
                m_newbuf[i] ^= key;
                key += delta;
                delta -= d_delta;
            }
            return new MemoryStream(m_newbuf);
        }
        public override void Extract(string Path)
        {
            File.WriteAllBytes(Path, m_buf);
        }

        public override object Data
        {
            get
            {
                if (m_encoding == null || m_buf == null) return string.Empty;
                return m_encoding.GetString(m_buf);
            }
        }
        public override void SetData(object Data)
        {
            m_buf = m_encoding.GetBytes(Data as string);
        }
        public void SetData(object Data, Encoding StringEncoding)
        {
            m_buf = StringEncoding.GetBytes(Data as string);
            m_encoding = StringEncoding;
        }

        public override void Dispose()
        {
            m_buf = null;
            m_encoding = null;
        }

        public override string ToString()
        {
            return Data as string;
        }
    }
    public class cv1 : cv0
    {
        public cv1()
            : base()
        {
            m_type = cvnType.CSV;
            m_records = new cv1DataCollection();
        }
        public cv1(string Path)
            : this()
        {
            Open(Path);
        }
        public cv1(Stream fp)
            : this()
        {
            Open(fp);
        }

        public string RawData
        {
            get { return base.Data as string; }
        }
        public override Encoding StringEncoding
        {
            get { return base.StringEncoding; }
            set
            {
                base.StringEncoding = value;
                ReloadRecords();
            }
        }
        private cv1DataCollection m_records;
        
        public override void SetData(object Data)
        {
            m_records = Data as cv1DataCollection;
            UpdateRawData();
            ReloadRecords();
        }
        public void ConvertEncoding(Encoding StringEncoding)
        {
            UpdateRawData(StringEncoding);
            this.StringEncoding = StringEncoding;
        }

        public override void Open(string Path)
        {
            Open(Path, Encoding.GetEncoding("Shift-JIS"));
        }
        public override void Open(string Path, Encoding StringEncoding)
        {
            base.Open(Path, StringEncoding);
            ReloadRecords();
        }
        public override void Open(Stream fp)
        {
            Open(fp, Encoding.GetEncoding("Shift-JIS"));
        }
        public override void Open(Stream fp, Encoding StringEncoding)
        {
            base.Open(fp, StringEncoding);
            ReloadRecords();
        }

        public override void SaveToFile(string Path)
        {
            UpdateRawData();
            base.SaveToFile(Path);
        }
        public override Stream ToStream()
        {
            UpdateRawData();
            return base.ToStream();
        }
        public override void Extract(string Path)
        {
            UpdateRawData();
            base.Extract(Path);
        }

        private void ReloadRecords()
        {
            m_records.Clear();
            string[] raw_records = RawData.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string i in raw_records)
            {
                if (i[0] == '#') continue;
                m_records.Add(new cv1DataLine(i));
            }
        }
        public void UpdateRawData()
        {
            base.SetData(m_records.ToString());
        }
        public void UpdateRawData(Encoding StringEncoding)
        {
            base.SetData(m_records.ToString(), StringEncoding);
        }

        public override object Data
        {
            get
            {
                return m_records;
            }
        }

        public override void Dispose()
        {
            m_records.Clear();
            base.Dispose();
        }

        public override string ToString()
        {
            return m_records.ToString();
        }
    }
    public class cv2 : cvnBase
    {
        private enum enum_graphic_format
        {
            WithPalette,
            General,
            Unknown,
        }

        private enum_graphic_format m_format;
        private byte m_raw_format;
        private int m_width_actual, m_height, m_width_data;
        private int m_unknown_field;
        private int m_length
        {
            get
            {
                int result = 0;
                switch (m_format)
                {
                    case enum_graphic_format.WithPalette:
                        result = m_width_data * m_height;
                        break;
                    case enum_graphic_format.General:
                        result = m_width_data * m_height * 4;
                        break;
                }
                return result;
            }
        }
        private int m_width_in_bytes
        {
            get
            {
                int result = 0;
                switch (m_format)
                {
                    case enum_graphic_format.WithPalette:
                        result = m_width_data;
                        break;
                    case enum_graphic_format.General:
                        result = m_width_data * 4;
                        break;
                }
                return result;
            }
        }
        private int m_actual_width_in_bytes
        {
            get
            {
                int result = 0;
                switch (m_format)
                {
                    case enum_graphic_format.WithPalette:
                        result = m_width_actual;
                        break;
                    case enum_graphic_format.General:
                        result = m_width_actual * 4;
                        break;
                }
                return result;
            }
        }
        private ushort[] m_palette;

        private Bitmap m_graphic;
        private bool m_generated_graphic;

        public int Width
        {
            get { return m_width_actual; }
        }
        public int Height
        {
            get { return m_height; }
        }

        public cv2()
            : base()
        {
            m_type = cvnType.Graphic;
            m_format = enum_graphic_format.Unknown;
            m_width_actual = m_height = m_width_data = 0;
            m_graphic = null;
        }
        public cv2(string Path)
            : this()
        {
            Open(Path);
        }
        public cv2(Stream fp)
            : this()
        {
            Open(fp);
        }

        public override object Data
        {
            get { return m_graphic; }
        }
        public override void SetData(object Data)
        {
            Bitmap _Data = Data as Bitmap;
            if (_Data.PixelFormat != PixelFormat.Format32bppArgb && _Data.PixelFormat != PixelFormat.Format8bppIndexed)
                throw new FormatException("지원하지 않는 포맷의 이미지입니다.");
            enum_graphic_format NewFormat = enum_graphic_format.Unknown;
            switch (_Data.PixelFormat)
            {
                case PixelFormat.Format8bppIndexed: NewFormat = enum_graphic_format.WithPalette; break;
                case PixelFormat.Format32bppArgb: NewFormat = enum_graphic_format.General; break;
            }
            if (m_format != NewFormat)
                throw new FormatException("포맷이 일치하지 않습니다.");
            if (m_width_actual != _Data.Width || m_height != _Data.Height)
                throw new FormatException("이미지 크기가 일치하지 않습니다.");
            m_graphic = _Data;
            m_generated_graphic = false;
        }
        
        public override void Open(string Path)
        {
            Open(Path, "");
        }
        public void Open(string Path, string PalettePath)
        {
            FileStream fp = new FileStream(Path, FileMode.Open);
            FileStream pp = null;
            if (PalettePath != "") pp = new FileStream(PalettePath, FileMode.Open);
            Open(fp, pp);
        }
        public override void Open(Stream fp)
        {
            Open(fp, null);
        }
        public void Open(Stream fp, Stream PalettePath)
        {
            fp.Seek(0, SeekOrigin.Begin);
            if (PalettePath != null) PalettePath.Seek(0, SeekOrigin.Begin);
            byte[] header = new byte[0x11];
            fp.Read(header, 0, 0x11);
            m_raw_format = header[0];
            switch (m_raw_format)
            {
                case 0x08:
                    m_format = enum_graphic_format.WithPalette;
                    break;
                case 0x18:
                case 0x20:
                    m_format = enum_graphic_format.General;
                    break;
                default:
                    m_format = enum_graphic_format.Unknown;
                    break;
            }
            byte[] buf = new byte[4];
            Array.Copy(header, 1, buf, 0, 4);
            m_width_actual = LittleEndian.FromEndian(buf);
            Array.Copy(header, 5, buf, 0, 4);
            m_height = LittleEndian.FromEndian(buf);
            Array.Copy(header, 9, buf, 0, 4);
            m_width_data = LittleEndian.FromEndian(buf);
            Array.Copy(header, 13, buf, 0, 4);
            m_unknown_field = LittleEndian.FromEndian(buf);

            PixelFormat image_format;
            switch (m_format)
            {
                case enum_graphic_format.General:
                    image_format = PixelFormat.Format32bppArgb;
                    break;
                case enum_graphic_format.WithPalette:
                    image_format = PixelFormat.Format16bppArgb1555;
                    break;
                default:
                    throw new FormatException("지원되지 않는 포맷의 cv2입니다.");
            }
            byte[] m_raw = new byte[m_length];
            fp.Read(m_raw, 0, m_length);
            m_graphic = new Bitmap(m_width_actual, m_height, image_format);
            m_generated_graphic = true;
            BitmapData raw_data;
            raw_data = m_graphic.LockBits(
                    new Rectangle(0, 0, m_width_actual, m_height),
                    ImageLockMode.WriteOnly,
                    image_format);

            if (m_format == enum_graphic_format.WithPalette)
            {
                if (PalettePath == null)
                {
                    m_graphic.Dispose();
                    throw new ArgumentException("팔레트 파일이 지정되지 않았습니다.", "PalettePath");
                }
                if (PalettePath.ReadByte() != 0x10)
                {
                    m_graphic.Dispose();
                    throw new FormatException("팔레트 파일이 올바르지 않습니다.");
                }
                m_palette = new ushort[256];
                byte[] palette_data = new byte[512];
                PalettePath.Read(palette_data, 0, 512);
                for (int i = 0; i < 512; i += 2)
                {
                    int t = palette_data[i] + (palette_data[i + 1] << 8);
                    m_palette[i / 2] = (ushort)t;
                }
            }

            int bitmap_len = raw_data.Stride * raw_data.Height;
            byte[] bitmap_buffer = new byte[bitmap_len];
            System.Runtime.InteropServices.Marshal.Copy(raw_data.Scan0, bitmap_buffer, 0, bitmap_len);
            for (int i = 0; i < m_height; i++)
            {
                int startAt_buf = i * m_width_in_bytes;
                int startAt_image = i * raw_data.Stride;
                for (int j = 0; j < m_actual_width_in_bytes; j++)
                {
                    switch (m_format)
                    {
                        case enum_graphic_format.General:
                            bitmap_buffer[startAt_image + j] = m_raw[startAt_buf + j];
                            break;
                        case enum_graphic_format.WithPalette:
                            byte index = m_raw[startAt_buf + j];
                            bitmap_buffer[startAt_image + j * 2] = (byte)(m_palette[index] & 0xff);
                            bitmap_buffer[startAt_image + j * 2 + 1] = (byte)(m_palette[index] >> 8);
                            break;
                    }
                }
            }
            System.Runtime.InteropServices.Marshal.Copy(bitmap_buffer, 0, raw_data.Scan0, bitmap_len);
            m_graphic.UnlockBits(raw_data);
        }
        
        public override void SaveToFile(string Path)
        {
            FileStream fp = new FileStream(Path, FileMode.Create);
            byte[] header = new byte[0x11];
            header[0] = m_raw_format;
            Array.Copy(LittleEndian.ToEndian(m_width_actual), 0, header, 1, 4);
            Array.Copy(LittleEndian.ToEndian(m_height), 0, header, 5, 4);
            Array.Copy(LittleEndian.ToEndian(m_width_data), 0, header, 9, 4);
            Array.Copy(LittleEndian.ToEndian(m_unknown_field), 0, header, 13, 4);
            fp.Write(header, 0, 0x11);

            BitmapData raw_data;
            switch (m_format)
            {
                case enum_graphic_format.General:
                    raw_data = m_graphic.LockBits(
                        new Rectangle(0, 0, m_width_actual, m_height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppArgb);
                    break;
                case enum_graphic_format.WithPalette:
                    raw_data = m_graphic.LockBits(
                        new Rectangle(0, 0, m_width_actual, m_height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format8bppIndexed);
                    break;
                default:
                    throw new FormatException("지원되지 않는 포맷의 cv2입니다.");
            }
            int bitmap_len = raw_data.Stride * raw_data.Height;
            byte[] bitmap_buffer = new byte[bitmap_len];
            System.Runtime.InteropServices.Marshal.Copy(raw_data.Scan0, bitmap_buffer, 0, bitmap_len);
            for (int i = 0; i < m_height; i++)
            {
                int startAt_image = i * raw_data.Stride;
                fp.Write(bitmap_buffer, startAt_image, m_actual_width_in_bytes);
                for (int j = m_actual_width_in_bytes; j < m_width_in_bytes; j++)
                    fp.WriteByte(0);
            }
            m_graphic.UnlockBits(raw_data);
            fp.Close();
        }
        public override Stream ToStream()
        {
            MemoryStream fp = new MemoryStream();
            byte[] header = new byte[0x11];
            header[0] = m_raw_format;
            Array.Copy(LittleEndian.ToEndian(m_width_actual), 0, header, 1, 4);
            Array.Copy(LittleEndian.ToEndian(m_height), 0, header, 5, 4);
            Array.Copy(LittleEndian.ToEndian(m_width_data), 0, header, 9, 4);
            Array.Copy(LittleEndian.ToEndian(m_unknown_field), 0, header, 13, 4);
            fp.Write(header, 0, 0x11);

            BitmapData raw_data;
            switch (m_format)
            {
                case enum_graphic_format.General:
                    raw_data = m_graphic.LockBits(
                        new Rectangle(0, 0, m_width_actual, m_height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppArgb);
                    break;
                case enum_graphic_format.WithPalette:
                    raw_data = m_graphic.LockBits(
                        new Rectangle(0, 0, m_width_actual, m_height),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format8bppIndexed);
                    break;
                default:
                    throw new FormatException("지원되지 않는 포맷의 cv2입니다.");
            }
            int bitmap_len = raw_data.Stride * raw_data.Height;
            byte[] bitmap_buffer = new byte[bitmap_len];
            System.Runtime.InteropServices.Marshal.Copy(raw_data.Scan0, bitmap_buffer, 0, bitmap_len);
            for (int i = 0; i < m_height; i++)
            {
                int startAt_image = i * raw_data.Stride;
                fp.Write(bitmap_buffer, startAt_image, m_actual_width_in_bytes);
                for (int j = m_actual_width_in_bytes; j < m_width_in_bytes; j++)
                    fp.WriteByte(0);
            }
            m_graphic.UnlockBits(raw_data);
            return new MemoryStream(fp.GetBuffer());
        }
        public override void Extract(string Path)
        {
            m_graphic.Save(Path);
        }

        public override void Dispose()
        {
            if (m_generated_graphic) m_graphic.Dispose();
        }

        public override string ToString()
        {
            return "cvn_helper.cv2";
        }
    }
    public class cv3 : cvnBase
    {
        public cv3()
            : base()
        {
            m_type = cvnType.Audio;
            m_sp = null;
            m_stream = null;
        }
        public cv3(string Path)
            : this()
        {
            Open(Path);
        }
        public cv3(Stream fp)
            : this()
        {
            Open(fp);
        }

        SoundPlayer m_sp;
        cv3Stream m_stream;

        public override object Data
        {
            get { return m_sp; }
        }

        public override void SetData(object Data)
        {
            throw new NotImplementedException();
        }

        public override void Open(Stream fp)
        {
            m_stream = new cv3Stream(fp);
            m_sp = new SoundPlayer(m_stream);
            m_sp.Load();
        }

        public override void SaveToFile(string Path)
        {
            int len = (int)m_stream.Length;
            byte[] buf = new byte[len - 0x1c];
            m_stream.Seek(0x14, SeekOrigin.Begin);
            m_stream.Read(buf, 0, 0x10);
            m_stream.Seek(0x2c, SeekOrigin.Begin);
            m_stream.Read(buf, 0x10, len - 0x2c);
            File.WriteAllBytes(Path, buf);
        }

        public override Stream ToStream()
        {
            int len = (int)m_stream.Length;
            byte[] buf = new byte[len - 0x1c];
            m_stream.Seek(0x14, SeekOrigin.Begin);
            m_stream.Read(buf, 0, 0x10);
            m_stream.Seek(0x2c, SeekOrigin.Begin);
            m_stream.Read(buf, 0x10, len - 0x2c);
            return new MemoryStream(buf);
        }

        public override void Extract(string Path)
        {
            m_stream.Seek(0, SeekOrigin.Begin);
            int len = (int)m_stream.Length;
            byte[] buffer = new byte[len];
            m_stream.Read(buffer, 0, len);
            File.WriteAllBytes(Path, buffer);
        }

        public override void Dispose()
        {
            m_sp.Stop();
            if (m_stream != null) m_stream.Dispose();
            if (m_sp != null) m_sp.Dispose();
        }
    }

    public class cvn
    {
        public static cvnBase Open(string Path, cvnType FileType = cvnType.Unknown, string PalettePath = "")
        {
            cvnType file_type = FileType;
            if (file_type == cvnType.Unknown)
            {
                string extension = Path.Substring(Path.IndexOf('.') + 1);
                switch (extension)
                {
                    case "cv0": file_type = cvnType.Text; break;
                    case "cv1": file_type = cvnType.CSV; break;
                    case "cv2": file_type = cvnType.Graphic; break;
                    case "cv3": file_type = cvnType.Audio; break;
                    default: throw new FormatException("지원하지 않는 확장자입니다.");
                }
            }
            switch (file_type)
            {
                case cvnType.Text: return new cv0(Path);
                case cvnType.CSV: return new cv1(Path);
                case cvnType.Graphic:
                    cv2 result = new cv2();
                    try
                    {
                        result.Open(Path, PalettePath);
                    }
                    catch (ArgumentException)
                    {
                        result = null;
                    }
                    return result;
                case cvnType.Audio: return new cv3(Path);
                default: return null;
            }
        }
        public static cvnBase Open(Stream fp, cvnType FileType, Stream pfp = null)
        {
            switch (FileType)
            {
                case cvnType.Text: return new cv0(fp);
                case cvnType.CSV: return new cv1(fp);
                case cvnType.Graphic:
                    cv2 result = new cv2();
                    try
                    {
                        result.Open(fp, pfp);
                    }
                    catch (System.ArgumentException)
                    {
                        result = null;
                    }
                    return result;
                case cvnType.Audio: return new cv3(fp);
                default: return null;
            }
        }
    }
}
