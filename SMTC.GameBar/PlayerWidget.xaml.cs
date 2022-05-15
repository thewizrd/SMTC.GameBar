using Microsoft.Gaming.XboxGameBar;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Control;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SMTC.GameBar
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayerWidget : Page
    {
        private XboxGameBarWidget widget;
        private PlayerViewModel PlayerViewModel { get; set; }

        private GlobalSystemMediaTransportControlsSessionManager SMTCManager;
        private IReadOnlyList<GlobalSystemMediaTransportControlsSession> MediaSessions;

        private GlobalSystemMediaTransportControlsSession MediaSession;
        private int SessionIndex = 0;

        public PlayerWidget()
        {
            this.InitializeComponent();

            PlayerViewModel = new PlayerViewModel();
            PlayerViewModel.PositionChanged += PlayerViewModel_PositionChanged;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            
            // you will need access to the XboxGameBarWidget, in this case it was passed as a parameter when navigating to the widget page, your implementation may differ.
            widget = e.Parameter as XboxGameBarWidget;

            StartService();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            StopService();
        }

        private async void StartService()
        {
            if (SMTCManager != null)
            {
                StopService();
            }

            SMTCManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            SMTCManager.SessionsChanged += SMTCManager_SessionsChanged;
            SMTCManager.CurrentSessionChanged += SMTCManager_CurrentSessionChanged;
            ReloadSessions(SMTCManager);
        }

        private void SMTCManager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            ReloadSessions(sender);
        }

        private void SMTCManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            // no-op
        }

        private async void ReloadSessions(GlobalSystemMediaTransportControlsSessionManager sessionManager)
        {
            MediaSessions = sessionManager?.GetSessions();
            SessionIndex = FindIndexOfCurrentSession(sessionManager.GetCurrentSession());

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var mediaSessionsCount = (MediaSessions?.Count ?? 1);

                if (mediaSessionsCount > 1)
                {
                    PlayerViewModel.ShowNextSession = SessionIndex + 1 < mediaSessionsCount;
                    PlayerViewModel.ShowPreviousSession = SessionIndex - 1 >= 0;
                }
                else
                {
                    PlayerViewModel.ShowNextSession = false;
                    PlayerViewModel.ShowPreviousSession = false;
                }
            });

            await LoadSession();
        }

        private int FindIndexOfCurrentSession(GlobalSystemMediaTransportControlsSession currentSession)
        {
            int i = 0;

            foreach (var session in MediaSessions)
            {
                if (Equals(currentSession.SourceAppUserModelId, session.SourceAppUserModelId))
                {
                    return i;
                }

                i++;
            }

            return 0;
        }

        private async Task LoadSession()
        {
            UnloadSession();

            MediaSession = MediaSessions.ElementAtOrDefault(SessionIndex);

            if (MediaSession != null)
            {
                MediaSession.PlaybackInfoChanged += MediaSession_PlaybackInfoChanged;
                MediaSession.MediaPropertiesChanged += MediaSession_MediaPropertiesChanged;
                MediaSession.TimelinePropertiesChanged += MediaSession_TimelinePropertiesChanged;

                await UpdatePlayer(MediaSession);
            }
        }

        private void StopService()
        {
            // Unregister events
            if (SMTCManager != null)
            {
                SMTCManager.SessionsChanged -= SMTCManager_SessionsChanged;
                SMTCManager.CurrentSessionChanged -= SMTCManager_CurrentSessionChanged;
            }

            SMTCManager = null;
            MediaSessions = null;
        }

        private void UnloadSession()
        {
            if (MediaSession != null)
            {
                MediaSession.PlaybackInfoChanged -= MediaSession_PlaybackInfoChanged;
                MediaSession.MediaPropertiesChanged -= MediaSession_MediaPropertiesChanged;
                MediaSession.TimelinePropertiesChanged -= MediaSession_TimelinePropertiesChanged;
                MediaSession = null;
            }
        }

        private async Task UpdatePlayer(GlobalSystemMediaTransportControlsSession mediaSession)
        {
            await UpdateMediaProperties(mediaSession);
            await UpdateTimeline(mediaSession);
            await UpdatePlaybackInfo(mediaSession);
        }

        private async Task UpdateMediaProperties(GlobalSystemMediaTransportControlsSession mediaSession)
        {
            var mediaProperties = await mediaSession.TryGetMediaPropertiesAsync();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                PlayerViewModel.Title = mediaProperties.Title;
                PlayerViewModel.Artist = mediaProperties.Artist;
                PlayerViewModel.Album = mediaProperties.AlbumTitle;
                await PlayerViewModel.UpdateThumbnail(mediaProperties.Thumbnail);
            });
        }

        private async Task UpdateTimeline(GlobalSystemMediaTransportControlsSession mediaSession)
        {
            var timelineProperties = mediaSession.GetTimelineProperties();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                PlayerViewModel.UpdateTimeline(timelineProperties);
            });
        }

        private async Task UpdatePlaybackInfo(GlobalSystemMediaTransportControlsSession mediaSession)
        {
            var playbackInfo = mediaSession.GetPlaybackInfo();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                PlayerViewModel.IsShuffleActive = playbackInfo.IsShuffleActive ?? false;
                PlayerViewModel.AutoRepeatMode = playbackInfo.AutoRepeatMode ?? Windows.Media.MediaPlaybackAutoRepeatMode.None;
                PlayerViewModel.IsPlaying = playbackInfo.PlaybackStatus switch
                {
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => true,
                    _ => false,
                };

                PlayerViewModel.IsShuffleEnabled = playbackInfo.Controls.IsShuffleEnabled;
                PlayerViewModel.IsRepeatEnabled = playbackInfo.Controls.IsRepeatEnabled;
                PlayerViewModel.IsPlaybackPositionEnabled = playbackInfo.Controls.IsPlaybackPositionEnabled;
                PlayerViewModel.IsPreviousEnabled = playbackInfo.Controls.IsPreviousEnabled;
                PlayerViewModel.IsNextEnabled = playbackInfo.Controls.IsNextEnabled;
                PlayerViewModel.IsPlayPauseEnabled = playbackInfo.Controls.IsPlayPauseToggleEnabled;
            });
        }

        private async void MediaSession_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            await UpdateTimeline(sender);
        }

        private async void MediaSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            await UpdateMediaProperties(sender);
        }

        private async void MediaSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            await UpdatePlaybackInfo(sender);
        }

        private async void PreviousSessionButton_Click(object sender, RoutedEventArgs e)
        {
            var mediaSessionsCount = (MediaSessions?.Count ?? 1);
            SessionIndex = SessionIndex - 1 < 0 ? mediaSessionsCount - 1 : SessionIndex - 1;

            if (mediaSessionsCount > 1)
            {
                PlayerViewModel.ShowNextSession = SessionIndex + 1 < mediaSessionsCount;
                PlayerViewModel.ShowPreviousSession = SessionIndex - 1 >= 0;
            }
            else
            {
                PlayerViewModel.ShowNextSession = false;
                PlayerViewModel.ShowPreviousSession = false;
            }

            await LoadSession();
        }

        private async void NextSessionButton_Click(object sender, RoutedEventArgs e)
        {
            var mediaSessionsCount = (MediaSessions?.Count ?? 1);
            SessionIndex = SessionIndex + 1 >= mediaSessionsCount ? 0 : SessionIndex + 1;

            if (mediaSessionsCount > 1)
            {
                PlayerViewModel.ShowNextSession = SessionIndex + 1 < mediaSessionsCount;
                PlayerViewModel.ShowPreviousSession = SessionIndex - 1 >= 0;
            }
            else
            {
                PlayerViewModel.ShowNextSession = false;
                PlayerViewModel.ShowPreviousSession = false;
            }

            await LoadSession();
        }

        private async void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            await MediaSession?.TrySkipPreviousAsync();
        }

        private async void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            var autoRepeatMode = PlayerViewModel.AutoRepeatMode switch
            {
                Windows.Media.MediaPlaybackAutoRepeatMode.None => Windows.Media.MediaPlaybackAutoRepeatMode.Track,
                Windows.Media.MediaPlaybackAutoRepeatMode.Track => Windows.Media.MediaPlaybackAutoRepeatMode.List,
                Windows.Media.MediaPlaybackAutoRepeatMode.List => Windows.Media.MediaPlaybackAutoRepeatMode.None,
                _ => Windows.Media.MediaPlaybackAutoRepeatMode.None,
            };

            await MediaSession?.TryChangeAutoRepeatModeAsync(autoRepeatMode);
        }

        private async void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            await MediaSession?.TryChangeShuffleActiveAsync(!PlayerViewModel.IsShuffleActive);
        }

        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerViewModel.IsPlaying)
            {
                await MediaSession?.TryPauseAsync();
            }
            else
            {
                await MediaSession?.TryPlayAsync();
            }
        }

        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            await MediaSession?.TrySkipNextAsync();
        }

        private async void PlayerViewModel_PositionChanged(object sender, double e)
        {
            await MediaSession?.TryChangePlaybackPositionAsync(TimeSpan.FromMilliseconds(e).Ticks);
        }
    }
}
