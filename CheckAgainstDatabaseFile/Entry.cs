using System;
using System.Windows;
using System.Windows.Media;

namespace CheckAgainstDatabaseFile
{
    public sealed class Entry
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
                if (this.FilenameL != null && this.FilenameR == null)
                {
                    return "=>";
                }
                if (this.FilenameL == null && this.FilenameR != null)
                {
                    return "<=";
                }

                if (this.FilenameL != null && this.FilenameR != null && this.SizeL != this.SizeR)
                {
                    return "!=";
                }
                if (this.FilenameL != null && this.FilenameR != null && this.LastWriteL != this.LastWriteR)
                {
                    return "!=";
                }
                if (this.FilenameL != null && this.FilenameR != null && this.HashL != this.HashR)
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
                switch (this.Eq)
                {
                    case "=>":
                        return new SolidColorBrush(Colors.DarkGreen);
                    case "<=":
                        return new SolidColorBrush(Colors.DarkBlue);
                    case "!=":
                        return new SolidColorBrush(Colors.Red);
                    default:
                        return new SolidColorBrush(Colors.Black);
                }
            }
        }

    }
}