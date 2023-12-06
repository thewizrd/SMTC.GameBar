using Microsoft.Gaming.XboxGameBar;
using NPSMLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimberLog;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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

        private NowPlayingSessionManager NPSManager;
        private IList<NowPlayingSession> MediaSessions;

        private NowPlayingSession MediaSession;
        private MediaPlaybackDataSource MediaPlaybackSource;
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

        private void StartService()
        {
            if (NPSManager != null)
            {
                StopService();
            }

            try
            {
                NPSManager = new NowPlayingSessionManager();
                NPSManager.SessionListChanged += NPSManager_SessionsChanged;
                ReloadSessions(NPSManager);
            }
            catch (Exception ex)
            {
                Timber.Log(LoggerLevel.Error, ex);
            }
        }

        private void NPSManager_SessionsChanged(object sender, NowPlayingSessionManagerEventArgs args)
        {
            if (args.NotificationType != NowPlayingSessionManagerNotificationType.CurrentSessionChanged)
            {
                ReloadSessions(sender as NowPlayingSessionManager ?? NPSManager);
            }
        }

        private async void ReloadSessions(NowPlayingSessionManager sessionManager)
        {
            MediaSessions = sessionManager?.GetSessions();
            SessionIndex = FindIndexOfCurrentSession(MediaSession ?? sessionManager.CurrentSession);

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

                PlayerViewModel.SessionsAvailable = (MediaSessions?.Count ?? 0) > 0;
            });

            await LoadSession();
        }

        private int FindIndexOfCurrentSession(NowPlayingSession currentSession)
        {
            int i = 0;

            foreach (var session in MediaSessions)
            {
                if (Equals(currentSession.SourceAppId, session.SourceAppId))
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
                MediaPlaybackSource = MediaSession.ActivateMediaPlaybackDataSource();
                MediaPlaybackSource.MediaPlaybackDataChanged += MediaPlaybackSource_MediaPlaybackDataChanged;

                await UpdatePlayer(MediaPlaybackSource);
            }
        }

        private void StopService()
        {
            // Unregister events
            if (NPSManager != null)
            {
                try
                {
                    NPSManager.SessionListChanged -= NPSManager_SessionsChanged;
                }
                catch (Exception ex)
                {
                    Timber.Log(LoggerLevel.Error, ex);
                }
            }

            NPSManager = null;
            MediaSessions = null;
        }

        private void UnloadSession()
        {
            if (MediaPlaybackSource != null)
            {
                try
                {
                    MediaPlaybackSource.MediaPlaybackDataChanged -= MediaPlaybackSource_MediaPlaybackDataChanged;
                }
                catch (Exception ex)
                {
                    Timber.Log(LoggerLevel.Error, ex);
                }
            }
            MediaPlaybackSource = null;
            MediaSession = null;
        }

        private async Task UpdatePlayer(MediaPlaybackDataSource source)
        {
            await UpdateMediaProperties(source);
            await UpdateTimeline(source);
            await UpdatePlaybackInfo(source);
        }

        private async Task UpdateMediaProperties(MediaPlaybackDataSource source)
        {
            var mediaObjectInfo = source.GetMediaObjectInfo();
            var thumbnailStream = source.GetThumbnailStream();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                PlayerViewModel.Title = mediaObjectInfo.Title;
                PlayerViewModel.Artist = mediaObjectInfo.Artist;
                PlayerViewModel.Album = mediaObjectInfo.AlbumTitle;

                if (string.IsNullOrWhiteSpace(PlayerViewModel.Artist) && !string.IsNullOrWhiteSpace(mediaObjectInfo.AlbumArtist))
                {
                    PlayerViewModel.Artist = mediaObjectInfo.AlbumArtist;
                }

                await PlayerViewModel.UpdateThumbnail(thumbnailStream);
            });
        }

        private async Task UpdateTimeline(MediaPlaybackDataSource source)
        {
            var timelineProperties = source.GetMediaTimelineProperties();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                PlayerViewModel.UpdateTimeline(timelineProperties);
            });
        }

        private async Task UpdatePlaybackInfo(MediaPlaybackDataSource source)
        {
            var playbackInfo = source.GetMediaPlaybackInfo();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var playerCapabilities = playbackInfo.PlaybackCaps;
                var playerValidProps = playbackInfo.PropsValid;

                PlayerViewModel.IsShuffleActive = playerValidProps.HasFlag(MediaPlaybackProps.ShuffleEnabled) ? playbackInfo.ShuffleEnabled : false;
                PlayerViewModel.AutoRepeatMode = playerValidProps.HasFlag(MediaPlaybackProps.AutoRepeatMode) ? playbackInfo.RepeatMode : MediaPlaybackRepeatMode.Unknown;
                PlayerViewModel.IsPlaying = (playerValidProps.HasFlag(MediaPlaybackProps.State) ? playbackInfo.PlaybackState : MediaPlaybackState.Unknown) switch
                {
                    MediaPlaybackState.Playing => true,
                    _ => false,
                };

                PlayerViewModel.IsShuffleEnabled = playerCapabilities.HasFlag(MediaPlaybackCapabilities.Shuffle);
                PlayerViewModel.IsRepeatEnabled = playerCapabilities.HasFlag(MediaPlaybackCapabilities.Repeat);
                PlayerViewModel.IsPlaybackPositionEnabled = playerCapabilities.HasFlag(MediaPlaybackCapabilities.PlaybackPosition);
                PlayerViewModel.IsPreviousEnabled = playerCapabilities.HasFlag(MediaPlaybackCapabilities.Previous);
                PlayerViewModel.IsNextEnabled = playerCapabilities.HasFlag(MediaPlaybackCapabilities.Next);
                PlayerViewModel.IsPlayPauseEnabled = playerCapabilities.HasFlag(MediaPlaybackCapabilities.PlayPauseToggle);
            });
        }

        private async void MediaPlaybackSource_MediaPlaybackDataChanged(object sender, MediaPlaybackDataChangedArgs e)
        {
            switch (e.DataChangedEvent)
            {
                case MediaPlaybackDataChangedEvent.PlaybackInfoChanged:
                    await UpdatePlaybackInfo(e.MediaPlaybackDataSource);
                    break;
                case MediaPlaybackDataChangedEvent.MediaInfoChanged:
                    await UpdateMediaProperties(e.MediaPlaybackDataSource);
                    break;
                case MediaPlaybackDataChangedEvent.TimelinePropertiesChanged:
                    await UpdateTimeline(e.MediaPlaybackDataSource);
                    break;
            }
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

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlaybackSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Previous);
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            var autoRepeatMode = PlayerViewModel.AutoRepeatMode switch
            {
                MediaPlaybackRepeatMode.None => MediaPlaybackRepeatMode.Track,
                MediaPlaybackRepeatMode.Track => MediaPlaybackRepeatMode.List,
                MediaPlaybackRepeatMode.List => MediaPlaybackRepeatMode.None,
                _ => MediaPlaybackRepeatMode.None,
            };

            MediaPlaybackSource?.SendRepeatModeChangeRequest(autoRepeatMode);
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlaybackSource?.SendShuffleEnabledChangeRequest(!PlayerViewModel.IsShuffleActive);
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (PlayerViewModel.IsPlaying)
            {
                MediaPlaybackSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Pause);
            }
            else
            {
                MediaPlaybackSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Play);
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            MediaPlaybackSource?.SendMediaPlaybackCommand(MediaPlaybackCommands.Next);
        }

        private void PlayerViewModel_PositionChanged(object sender, double e)
        {
            MediaPlaybackSource?.SendPlaybackPositionChangeRequest(TimeSpan.FromMilliseconds(e));
        }
    }
}
