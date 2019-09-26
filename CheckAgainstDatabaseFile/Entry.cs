using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace CheckAgainstDatabaseFile
{
    public class Entry
    {
        public string FilenameL { get; }
        public string FilenameR{ get; }
        public long? SizeL{ get; }
        public long? SizeR{ get; }
        public ulong? HashL{ get; }
        public ulong? HashR{ get; }
        public DateTime? LastWriteL{ get; }
        public DateTime? LastWriteR{ get; }

        public Entry(string filenameL, string filenameR, long? sizeL, long? sizeR, DateTime? lastWriteL, DateTime? lastWriteR, ulong? hashL, ulong? hashR)
        {
            this.FilenameL = filenameL;
            this.FilenameR = filenameR;
            this.SizeL = sizeL;
            this.SizeR = sizeR;
            this.LastWriteL = lastWriteL;
            this.LastWriteR = lastWriteR;
            this.HashL = hashL;
            this.HashR = hashR;
        }

        public Visibility LeftVisible => this.FilenameL != null ? Visibility.Visible : Visibility.Hidden;
        public Visibility RightVisible => this.FilenameR != null ? Visibility.Visible : Visibility.Hidden;

        public string Eq
        {
            get
            {
                if (FilenameL != null && FilenameR == null)
                {
                    return "=>";
                }
                if (FilenameL == null && FilenameR != null)
                {
                    return "<=";
                }

                if (FilenameL != null && FilenameR != null && SizeL != SizeR)
                {
                    return "!=";
                }
                if (FilenameL != null && FilenameR != null && LastWriteL != LastWriteR)
                {
                    return "!=";
                }
                if (FilenameL != null && FilenameR != null && HashL != HashR)
                {
                    return "!=";
                }

                return "=";
            }
        }

        public Brush Color
        {
            get
            {
                if (this.Eq == "=>") return new SolidColorBrush(Colors.DarkGreen);
                if (this.Eq == "<=") return new SolidColorBrush(Colors.DarkBlue);
                if (this.Eq == "!=") return new SolidColorBrush(Colors.Red);
                return new SolidColorBrush(Colors.Black);
            }
        }

    }
}