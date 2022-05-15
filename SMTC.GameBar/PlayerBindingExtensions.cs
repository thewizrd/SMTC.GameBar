using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.UI.Xaml.Controls;

namespace SMTC.GameBar
{
    public static class PlayerBindingExtensions
    {
        public static string IsPlayingGlyph(bool playing)
        {
            return playing ? "\uF8AE" : "\uF5B0";
        }
        public static string RepeatModeGlyph(MediaPlaybackAutoRepeatMode repeatMode)
        {
            return repeatMode switch
            {
                MediaPlaybackAutoRepeatMode.None => "\uF5E7",
                MediaPlaybackAutoRepeatMode.Track => "\uE8ED",
                MediaPlaybackAutoRepeatMode.List => "\uE8EE",
                _ => "\uF5E7",
            };
        }
    }
}
