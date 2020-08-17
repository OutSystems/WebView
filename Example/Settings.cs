using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Example {
    public static class Settings {
        public static event Action StylePreferenceChanged;

        public static bool isBorderLessPreference = true;
        public static bool IsBorderLessPreference {
            get => isBorderLessPreference;
            set {
                isBorderLessPreference = value;
                StylePreferenceChanged?.Invoke();
            }
        }
    }
}
