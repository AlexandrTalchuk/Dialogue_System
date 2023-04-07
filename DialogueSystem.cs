using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using MergeMarines._Application.Scripts.Extensions;
using MergeMarines.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MergeMarines._Application.Scripts.GameUI
{
    [RequireComponent(typeof(RectTransform))]
    public class DialogueSystem : MonoBehaviour
    {
        private const char Separator = '_';
        private const string Intro = "Intro";
        private const string Final = "Final";
        
        private const float AnimationDuration = 0.5f;
        
        [SerializeField]
        private TextMeshProUGUI _dialogueText;
        [SerializeField]
        private TextMeshProUGUI _nameText;
        [SerializeField]
        private TextMeshProUGUI _skipText;
        [SerializeField]
        private Image _characterImage;
        [SerializeField]
        private CanvasGroup _contentGroup;
        [SerializeField]
        private RectTransform _contentRect;
        [SerializeField]
        private Button _backgroundNextButton;
        [SerializeField]
        private Button _nextButton;
        [SerializeField]
        private Button _skipButton;
        [SerializeField]
        private RectTransform _scalableContainer;

        [Header("Settings")]
        [SerializeField]
        private float _textSpeedCharPerSec = 30;

        private int _keyIndex;
        private List<string> _keys;
        private bool _isInitialized;
        private bool _isTextAnimating;
        private string _lastCharacterKey;
        private AudioSource _loopedAudio;
        private Sequence _contentAnimation;
        private Sequence _characterAnimation;
        private Vector3 _defaultCharacterScale;
        private Vector2 _defaultContentPosition;
        private Vector2 _defaultCharacterPosition;
        private CancellationTokenSource _cancellationTokenSource;

        private void Awake()
        {
            _scalableContainer.offsetMin = new Vector2(0, -UIUtils.ButtomOffset);
        }

        private void Start()
        {
            _nextButton.onClick.AddListener(OnNextButtonClick);
            _skipButton.onClick.AddListener(OnSkipButtonClick);
            _backgroundNextButton.onClick.AddListener(OnNextButtonClick);

            _skipText.text = Strings.SkipTitle;
        }

        private void OnDisable()
        {
            if (_isTextAnimating) 
                StopTextAnimation();
                
            AudioManager.FreeLoopedUI(_loopedAudio);
            _cancellationTokenSource.Cancel();
        }

        public void Setup(string questId, bool isFinal)
        {
            Activate(GetDialogueKeyPrefix(questId, isFinal));
        }
        
        public void Setup(Window currentWindow, DungeonType dungeonType, int stageIndex)
        {
            Activate(GetDungeonKeyPrefix(currentWindow, dungeonType, stageIndex));
        }

        private void Activate(string prefixKey)
        {
            _keyIndex = 0;
            _keys = LocalizationExtentions.GetKeys(prefixKey);
            
            if (_keys.IsNullOrEmpty())
                return;

            _loopedAudio = AudioManager.PlayLoopedUI(SoundType.TextTapping);
            _loopedAudio.Stop();

            Initialize();
            ShowAppearance();
            ShowTextAnimation();
        }

        private void Initialize()
        {
            if (_isInitialized)
                return;
            
            RectTransform rect = (RectTransform) _characterImage.transform;
            _defaultCharacterPosition = rect.anchoredPosition;
            _defaultCharacterScale = rect.localScale;

            _defaultContentPosition = _contentRect.anchoredPosition;
            
            _characterAnimation = DOTween.Sequence()
                .Append(rect.DOAnchorPos(_defaultCharacterPosition, AnimationDuration)
                    .SetEase(Ease.OutBack)
                    .From(new Vector2(-200, _defaultCharacterPosition.y)))
                .Join(rect.DOScale(_defaultCharacterScale, AnimationDuration)
                    .SetEase(Ease.OutBack)
                    .From(Vector3.zero))
                .Join(_characterImage.DOFade(1f, AnimationDuration)
                    .SetEase(Ease.OutSine)
                    .From(0))
                .SetAutoKill(false)
                .SetTarget(_characterImage);

            _contentAnimation = DOTween.Sequence()
                .Append(_contentRect.DOAnchorPos(_defaultContentPosition, AnimationDuration)
                    .SetEase(Ease.OutSine)
                    .From(Vector2.down * 500))
                .Join(_contentGroup.DOFade(1f, AnimationDuration)
                    .SetEase(Ease.OutSine)
                    .From(0))
                .SetAutoKill(false)
                .SetTarget(_contentRect);

            _characterAnimation.Rewind();
            _contentAnimation.Rewind();
            
            _isInitialized = true;
        }

        private void ShowAppearance()
        {
            _nextButton.enabled = true;
            _skipButton.enabled = true;
            _backgroundNextButton.enabled = true;
            
            gameObject.SetActive(true);
            
            _characterAnimation.Restart();
            _contentAnimation.Restart();

            _characterAnimation.OnRewind(null);
            _characterAnimation.Play();
            _contentAnimation.Play();
            
            AudioManager.Play2D(SoundType.DialogueCharacterShow);
        }

        private void ShowHide()
        {
            _nextButton.enabled = false;
            _skipButton.enabled = false;
            _backgroundNextButton.enabled = false;

            _characterAnimation.OnRewind(() => gameObject.SetActive(false));
            _characterAnimation.PlayBackwards();
            _contentAnimation.PlayBackwards();
            
            AudioManager.Play2D(SoundType.FinishDialogue);
        }

        private void ShowTextAnimation()
        {
            if (_keyIndex >= _keys.Count)
            {
                ShowHide();
                return;
            }
            
            if (_isTextAnimating) 
                StopTextAnimation();

            _cancellationTokenSource = new CancellationTokenSource();
            TextAppearance(_cancellationTokenSource.Token).Forget(); 

            _loopedAudio.SetRandomPlayTime().Play();

            string nameKey = _keys[_keyIndex].Split(Separator)[^2];

            if (nameKey != _lastCharacterKey)
            {
                _lastCharacterKey = nameKey;
                
                string unitName =  nameKey.LocalizedUnitByName();
                _nameText.text = unitName;
                _characterImage.sprite = IconManager.GetIcon(unitName);
               _characterImage.SetNativeSize();
                
                if (_characterAnimation.IsActive())
                    _characterAnimation.Complete(false);

                _characterAnimation.OnRewind(null);
                _characterAnimation.Restart();
                _characterAnimation.Play();
                
                AudioManager.Play2D(SoundType.DialogueCharacterShow);
            }
        }

        private void StopTextAnimation()
        {
            _cancellationTokenSource.Cancel();
            _isTextAnimating = false;
            _loopedAudio.Stop();
        }
        
        private async UniTask TextAppearance(CancellationToken ct)
        {
            string text = _keys[_keyIndex].Localized();
            float time = 0;
            int charsCount = 0;
            int length = text.Length;
            _isTextAnimating = true;

            while (charsCount < length)
            {
                time += Time.deltaTime;

                charsCount = Mathf.Min((time * _textSpeedCharPerSec).RoundArithmeticToInt(), length);
                _dialogueText.text = text[..charsCount];
                
                await UniTask.Yield(ct);
            }

            _isTextAnimating = false;
            _loopedAudio.Stop();
        }

        private void OnNextButtonClick()
        {
            if (_isTextAnimating)
            {
                StopTextAnimation();
                _dialogueText.text = _keys[_keyIndex].Localized();
            }
            else
            {
                _keyIndex++;
                ShowTextAnimation();
            }
        }

        private void OnSkipButtonClick()
        {
            ShowHide();
        }

        private string GetDungeonKeyPrefix(Window currentWindow, DungeonType type, int stage)
        {
            var window = currentWindow.GetType().Name.
                Replace(currentWindow.IsPopup ? "Popup" : nameof(Window), "");
            
            return $"{window}{Separator}{type}{Separator}{stage}{Separator}";
        } 
        
        private string GetDialogueKeyPrefix(string questId, bool isFinal)
        {
            return $"{questId}{Separator}{(isFinal ? Final : Intro)}{Separator}";
        }

        public void ForceHide()
        {
            _nextButton.enabled = false;
            _skipButton.enabled = false;
            _backgroundNextButton.enabled = false;
            gameObject.SetActive(false);
            _characterAnimation.PlayBackwards();
            _contentAnimation.PlayBackwards();
        }
    }
}