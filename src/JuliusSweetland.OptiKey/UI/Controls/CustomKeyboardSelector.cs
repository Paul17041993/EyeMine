using System;
using JuliusSweetland.OptiKey.UI.ViewModels.Keyboards.Base;

namespace JuliusSweetland.OptiKey.UI.ViewModels.Keyboards
{
    public class CustomKeyboardSelector : BackActionKeyboard
    {
        public CustomKeyboardSelector(Action backAction)
            : base(backAction)
        {
        }
    }
}
