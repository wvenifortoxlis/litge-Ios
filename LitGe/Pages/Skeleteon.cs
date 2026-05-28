using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LitGe.Pages
{
    public partial class Skeleteon : BoxView
    {
        public Skeleteon()
        {
            Dispatcher.StartTimer(
                TimeSpan.FromSeconds(1),
                () =>
                {
                    this.FadeTo(0.2, 500, Easing.CubicIn)
                        .ContinueWith(
                            (x) =>
                            {
                                this.FadeTo(1, 500, Easing.CubicOut);
                            }
                        );

                    return true;
                }
            );
        }
    }
}
