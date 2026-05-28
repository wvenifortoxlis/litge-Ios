using Plugin.Maui.Audio;

namespace LitGe.Lib
{
    public class AudioPlayer : IDisposable
    {
        public class SkipEventArgs(int seconds) : EventArgs
        {
            public int Seconds { get; } = seconds;
        }

        public class PlayEventArgs(double position = 0) : EventArgs
        {
            public double Position { get; } = position;
        }

        public class AudioStateEventArgs(bool isPLaying) : EventArgs
        {
            public bool IsPLaying { get; } = isPLaying;
        }

        private MemoryStream _content;
        private IAudioPlayer _player;

        public bool IsPlaying { get; private set; }

        public event EventHandler<PlayEventArgs>? PlayEvent;
        public event EventHandler? PauseEvent;
        public event EventHandler<SkipEventArgs>? SkipForwardEvent;
        public event EventHandler<SkipEventArgs>? SkipBackwardEvent;
        public event EventHandler? PlaybackEnded;
        public event EventHandler<AudioStateEventArgs>? AudioStateEvent;

        public AudioPlayer(byte[] content)
        {
            this._content = new MemoryStream(content);
            this._player = AudioManager.Current.CreatePlayer(_content);
            this._player.Loop = false;

            this._player.PlaybackEnded += _player_PlaybackEnded;

            this.PlayEvent += AudioPlayer_PlayEvent;
            this.PauseEvent += AudioPlayer_PauseEvent;
            this.SkipBackwardEvent += AudioPlayer_SkipBackwardEvent;
            this.SkipForwardEvent += AudioPlayer_SkipForwardEvent;
        }

        private void _player_PlaybackEnded(object? sender, EventArgs e) =>
            this.PlaybackEnded?.Invoke(this, e);

        public void OnPlay(PlayEventArgs eventArgs) => this.PlayEvent?.Invoke(this, eventArgs);

        public void OnPause() => this.PauseEvent?.Invoke(this, EventArgs.Empty);

        public void OnSkipForward(int seconds = 10) =>
            this.SkipForwardEvent?.Invoke(this, new SkipEventArgs(seconds));

        public void OnSkipBackward(int seconds = 10) =>
            this.SkipBackwardEvent?.Invoke(this, new SkipEventArgs(seconds));

        public void Stop()
        {
            this.IsPlaying = false;
            this._player.Stop();
            this.AudioStateEvent?.Invoke(this, new AudioStateEventArgs(false));
        }

        public void Goto(double position) => this.OnPlay(new PlayEventArgs(position));

        public void SetAudioContent(byte[] bytes, PlayEventArgs args)
        {
            if (this._player.IsPlaying)
                OnPause();

            this._player.PlaybackEnded -= _player_PlaybackEnded;
            _player.Dispose();
            _content.Dispose();

            _content = new MemoryStream(bytes);
            _player = AudioManager.Current.CreatePlayer(_content);
            this._player.Loop = false;
            this._player.PlaybackEnded += _player_PlaybackEnded;

            OnPlay(args);
        }

        public double GetCurrentPosition() =>
            Math.Min(this._player.CurrentPosition, this._player.Duration);

        public double GetTotalLength() => this._player.Duration;

        private void AudioPlayer_SkipForwardEvent(object? sender, SkipEventArgs e) =>
            this.OnPlay(new PlayEventArgs(this._player.CurrentPosition + e.Seconds));

        private void AudioPlayer_SkipBackwardEvent(object? sender, SkipEventArgs e) =>
            this.OnPlay(new PlayEventArgs(this._player.CurrentPosition - e.Seconds));

        private void AudioPlayer_PauseEvent(object? sender, EventArgs e)
        {
            this.IsPlaying = false;
            this._player.Pause();
            this.AudioStateEvent?.Invoke(this, new AudioStateEventArgs(false));
        }

        private void AudioPlayer_PlayEvent(object? sender, PlayEventArgs e)
        {
            this.IsPlaying = true;
            this._player.Play();
            this._player.Seek(e.Position);
            this.AudioStateEvent?.Invoke(this, new AudioStateEventArgs(true));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (_player != null) {
                try {
                    _player.Stop();
                    _player.PlaybackEnded -= this._player_PlaybackEnded;
                    _player.Dispose();
                } catch { }
            }

            _content?.Dispose();

            PlayEvent = null;
            PauseEvent = null;
            SkipForwardEvent = null;
            SkipBackwardEvent = null;
        }
    }
}
