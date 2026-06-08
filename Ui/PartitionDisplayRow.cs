using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Xiaomi_Flash.Ui
{
    public class PartitionDisplayRow : INotifyPropertyChanged
    {
        string index = "";
        string name = "";
        string imageFile = "";
        string size = "";
        string progressStr = "";
        string status = "";

        public string Index
        {
            get => index;
            set => SetField(ref index, value);
        }

        public string Name
        {
            get => name;
            set => SetField(ref name, value);
        }

        public string ImageFile
        {
            get => imageFile;
            set => SetField(ref imageFile, value);
        }

        public string Size
        {
            get => size;
            set => SetField(ref size, value);
        }

        public string ProgressStr
        {
            get => progressStr;
            set => SetField(ref progressStr, value);
        }

        public string Status
        {
            get => status;
            set => SetField(ref status, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        void SetField(ref string field, string value, [CallerMemberName] string? propertyName = null)
        {
            if (field == value)
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
