using System;

namespace ProjectC.UIv2.Screens.Bag
{
    public class BagViewModel
    {
        public event Action<string> OnStatusChanged;
        private string _status = "ready";
        public string Status => _status;
        public void SetStatus(string value)
        {
            _status = value;
            OnStatusChanged?.Invoke(value);
        }
    }
}
