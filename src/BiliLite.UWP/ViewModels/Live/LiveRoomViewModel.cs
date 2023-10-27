﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Input;
using Windows.UI.Xaml;
using BiliLite.Extensions;
using BiliLite.Models;
using BiliLite.Models.Common;
using BiliLite.Models.Common.Live;
using BiliLite.Models.Exceptions;
using BiliLite.Models.Requests.Api;
using BiliLite.Models.Requests.Api.Live;
using BiliLite.Modules;
using BiliLite.Modules.Live;
using BiliLite.Services;
using BiliLite.ViewModels.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PropertyChanged;

namespace BiliLite.ViewModels.Live
{
    public class LiveRoomViewModel : BaseViewModel
    {
        #region Fields

        private static readonly ILogger _logger = GlobalLogger.FromCurrentType();
        private readonly LiveRoomAPI m_liveRoomApi;
        private readonly PlayerAPI m_playerApi;
        private System.Threading.CancellationTokenSource m_cancelSource;
        private Modules.Live.LiveMessage m_liveMessage;
        private readonly Timer m_timerBox;
        private readonly Timer m_timerAutoHideGift;
        private int m_hideGiftFlag = 1;
        private List<LiveGiftItem> m_allGifts = new List<LiveGiftItem>();
        private readonly Timer m_timer;

        #endregion

        #region Constructors

        public LiveRoomViewModel()
        {
            m_liveRoomApi = new LiveRoomAPI();
            m_playerApi = new PlayerAPI();
            //liveMessage = new Live.LiveMessage();
            AnchorLotteryViewModel = new LiveRoomAnchorLotteryViewModel();
            MessageCenter.LoginedEvent += MessageCenter_LoginedEvent;
            MessageCenter.LogoutedEvent += MessageCenter_LogoutedEvent;
            Logined = SettingService.Account.Logined;
            Messages = new ObservableCollection<DanmuMsgModel>();
            GiftMessage = new ObservableCollection<GiftMsgModel>();
            Guards = new ObservableCollection<LiveGuardRankItem>();
            BagGifts = new ObservableCollection<LiveGiftItem>();
            SuperChats = new ObservableCollection<SuperChatMsgModel>();
            m_timer = new Timer(1000);
            m_timerBox = new Timer(1000);
            m_timerAutoHideGift = new Timer(1000);
            m_timer.Elapsed += Timer_Elapsed;
            m_timerBox.Elapsed += Timer_box_Elapsed;
            m_timerAutoHideGift.Elapsed += Timer_auto_hide_gift_Elapsed;

            LoadMoreGuardCommand = new RelayCommand(LoadMoreGuardList);
            ShowBagCommand = new RelayCommand(SetShowBag);
            RefreshBagCommand = new RelayCommand(RefreshBag);
        }

        #endregion

        #region Properties

        public ICommand LoadMoreGuardCommand { get; private set; }

        public ICommand ShowBagCommand { get; private set; }

        public ICommand RefreshBagCommand { get; private set; }

        public LiveRoomAnchorLotteryViewModel AnchorLotteryViewModel { get; set; }

        [DoNotNotify]
        public static List<LiveTitleModel> Titles { get; set; }

        public bool Logined { get; set; }

        /// <summary>
        /// 直播ID
        /// </summary>
        [DoNotNotify]
        public int RoomID { get; set; }

        /// <summary>
        /// 房间标题
        /// </summary>
        [DoNotNotify]
        public string RoomTitle { get; set; }

        [DoNotNotify]
        public ObservableCollection<DanmuMsgModel> Messages { get; set; }

        [DoNotNotify]
        public ObservableCollection<GiftMsgModel> GiftMessage { get; set; }

        [DoNotNotify]
        public ObservableCollection<LiveGiftItem> BagGifts { get; set; }

        [DoNotNotify]
        public bool ReceiveWelcomeMsg { get; set; } = true;

        [DoNotNotify]
        public bool ReceiveLotteryMsg { get; set; } = true;

        [DoNotNotify]
        public bool ReceiveGiftMsg { get; set; } = true;

        public bool ShowGiftMessage { get; set; }

        /// <summary>
        /// 人气值
        /// </summary>
        public int Online { get; set; }

        public bool Loading { get; set; } = true;

        public bool Attention { get; set; }

        public bool ShowBag { get; set; }

        public List<LiveRoomRankViewModel> Ranks { get; set; }
        
        public LiveRoomRankViewModel SelectRank { get; set; }

        [DoNotNotify]
        public ObservableCollection<LiveGuardRankItem> Guards { get; set; }

        [DoNotNotify]
        public ObservableCollection<SuperChatMsgModel> SuperChats { get; set; }

        [DoNotNotify]
        public LiveRoomWebUrlQualityDescriptionItemModel CurrentQn { get; set; }

        public List<LiveRoomWebUrlQualityDescriptionItemModel> Qualites { get; set; }

        public List<LiveGiftItem> Gifts { get; set; }

        public List<LiveBagGiftItem> Bag { get; set; }

        [DoNotNotify]
        public List<LiveRoomRealPlayUrlsModel> Urls { get; set; }

        public LiveWalletInfo WalletInfo { get; set; }

        public LiveInfoModel LiveInfo { get; set; }

        public LiveAnchorProfile Profile { get; set; }

        public bool Liveing { get; set; }

        public string LiveTime { get; set; }

        [DoNotNotify]
        public int CleanCount { get; set; } = 200;

        [DoNotNotify]
        public int GuardPage { get; set; } = 1;

        public bool LoadingGuard { get; set; } = true;

        public bool LoadMoreGuard { get; set; }

        public bool ShowBox { get; set; }

        public bool OpenBox { get; set; }

        public string BoxTime { get; set; } = "--:--";

        [DoNotNotify]
        public DateTime freeSilverTime { get; set; }

        [DoNotNotify]
        public bool AutoReceiveFreeSilver { get; set; }

        #endregion

        #region Events

        public event EventHandler<LiveRoomPlayUrlModel> ChangedPlayUrl;

        public event EventHandler<LiveRoomEndAnchorLotteryInfoModel> LotteryEnd;

        public event EventHandler<DanmuMsgModel> AddNewDanmu;

        #endregion

        #region Private Methods

        private void LiveMessage_NewMessage(MessageType type, object message)
        {
            if (Messages == null) return;
            switch (type)
            {
                case MessageType.ConnectSuccess:
                    Messages.Add(new DanmuMsgModel()
                    {
                        UserName = message.ToString(),
                    });
                    break;
                case MessageType.Online:
                    Online = (int)message;
                    break;
                case MessageType.Danmu:
                    {
                        var m = message as DanmuMsgModel;
                        m.ShowUserLevel = Visibility.Visible;
                        if (Messages.Count >= CleanCount)
                        {
                            Messages.Clear();
                        }
                        Messages.Add(m);
                        AddNewDanmu?.Invoke(this, m);
                    }
                    break;
                case MessageType.Gift:
                    {
                        if (!ReceiveGiftMsg)
                        {
                            return;
                        }
                        if (GiftMessage.Count >= 2)
                        {
                            GiftMessage.RemoveAt(0);
                        }
                        ShowGiftMessage = true;
                        m_hideGiftFlag = 1;
                        var info = message as GiftMsgModel;
                        info.Gif = m_allGifts.FirstOrDefault(x => x.Id == info.GiftId)?.Gif ?? Constants.App.TRANSPARENT_IMAGE;
                        GiftMessage.Add(info);
                        if (!m_timerAutoHideGift.Enabled)
                        {
                            m_timerAutoHideGift.Start();
                        }
                    }

                    break;
                case MessageType.Welcome:
                    {
                        var info = message as WelcomeMsgModel;
                        if (ReceiveWelcomeMsg)
                        {
                            Messages.Add(new DanmuMsgModel()
                            {
                                UserName = info.UserName,
                                UserNameColor = "#FFFF69B4",//Colors.HotPink
                                Text = " 进入直播间"
                            });
                        }
                    }
                    break;
                case MessageType.WelcomeGuard:
                    {
                        var info = message as WelcomeMsgModel;
                        if (ReceiveWelcomeMsg)
                        {
                            Messages.Add(new DanmuMsgModel()
                            {
                                UserName = info.UserName,
                                UserNameColor = "#FFFF69B4",//Colors.HotPink
                                Text = " (舰长)进入直播间"
                            });
                        }
                    }
                    break;
                case MessageType.SystemMsg:
                    break;
                case MessageType.SuperChat:
                case MessageType.SuperChatJpn:
                    SuperChats.Add(message as SuperChatMsgModel);
                    break;
                case MessageType.AnchorLotteryStart:
                    if (ReceiveLotteryMsg)
                    {
                        var info = message.ToString();
                        AnchorLotteryViewModel.SetLotteryInfo(JsonConvert.DeserializeObject<LiveRoomAnchorLotteryInfoModel>(info));
                    }
                    break;
                case MessageType.AnchorLotteryEnd:
                    break;
                case MessageType.AnchorLotteryAward:
                    if (ReceiveLotteryMsg)
                    {
                        var info = JsonConvert.DeserializeObject<LiveRoomEndAnchorLotteryInfoModel>(message.ToString());
                        LotteryEnd?.Invoke(this, info);
                    }
                    break;

                case MessageType.GuardBuy:
                    {
                        var info = message as GuardBuyMsgModel;
                        Messages.Add(new DanmuMsgModel()
                        {
                            UserName = info.UserName,
                            UserNameColor = "#FFFF69B4",//Colors.HotPink
                            Text = $"成为了{info.GiftName}"
                        });
                        // 刷新舰队列表
                        _ = GetGuardList();
                    }
                    break;
                case MessageType.RoomChange:
                    {
                        var info = message as RoomChangeMsgModel;
                        RoomTitle = info.Title;
                    }
                    break;
                default:
                    break;
            }
        }

        private async void Timer_auto_hide_gift_Elapsed(object sender, ElapsedEventArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (GiftMessage == null || GiftMessage.Count == 0) return;
                if (m_hideGiftFlag >= 5)
                {
                    ShowGiftMessage = false;
                    GiftMessage.Clear();
                }
                else
                {
                    m_hideGiftFlag++;
                }
            });
        }

        private void MessageCenter_LogoutedEvent(object sender, EventArgs e)
        {
            Logined = false;
            m_timerBox.Stop();
            ShowBox = false;
            OpenBox = false;
        }

        private async void MessageCenter_LoginedEvent(object sender, object e)
        {
            Logined = true;
            await LoadWalletInfo();
            await LoadBag();
            //await GetFreeSilverTime();
        }

        private async void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                if (LiveInfo == null && !Liveing)
                {
                    await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        LiveTime = "";
                    });
                    return;
                }
                var start_time = TimeExtensions.TimestampToDatetime(LiveInfo.RoomInfo.LiveStartTime);
                var ts = DateTime.Now - start_time;

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    for (int i = 0; i < SuperChats.Count; i++)
                    {
                        var item = SuperChats[i];
                        if (item.time <= 0)
                        {
                            SuperChats.Remove(item);
                        }
                        else
                        {
                            item.time -= 1;
                        }
                    }

                    LiveTime = ts.ToString(@"hh\:mm\:ss");
                });
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message, ex);
            }
        }

        private async void ReceiveMessage(int roomId)
        {
            try
            {
                var uid = 0;
                if (SettingService.Account.Logined)
                {
                    uid = SettingService.Account.UserID;
                }
                m_liveMessage ??= new LiveMessage();

                var buvidResults = await m_liveRoomApi.GetBuvid().Request();
                var buvidData = await buvidResults.GetJson<ApiDataModel<LiveBuvidModel>>();
                var buvid = buvidData.data.B3;

                var danmukuResults = await m_liveRoomApi.GetDanmukuInfo(roomId).Request();
                var danmukuData = await danmukuResults.GetJson<ApiDataModel<LiveDanmukuInfoModel>>();
                var token = danmukuData.data.Token;
                var host = danmukuData.data.HostList[0].Host;

                await m_liveMessage.Connect(roomId, uid, token, buvid, host, m_cancelSource.Token);
            }
            catch (TaskCanceledException)
            {
                Messages.Add(new DanmuMsgModel()
                {
                    UserName = "取消连接"
                });
            }
            catch (Exception ex)
            {
                Messages?.Add(new DanmuMsgModel()
                {
                    UserName = "连接失败:" + ex.Message
                });
            }

        }

        private async void Timer_box_Elapsed(object sender, ElapsedEventArgs e)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                if (DateTime.Now >= freeSilverTime)
                {
                    ShowBox = false;
                    OpenBox = true;
                    m_timerBox.Stop();
                    if (AutoReceiveFreeSilver)
                    {
                        await GetFreeSilver();
                    }
                }
                else
                {
                    OpenBox = false;
                    BoxTime = (freeSilverTime - DateTime.Now).ToString(@"mm\:ss");
                }
            });
        }

        private void SetShowBag()
        {
            if (!ShowBag && !SettingService.Account.Logined)
            {
                Notify.ShowMessageToast("请先登录");
                return;
            }
            ShowBag = !ShowBag;
        }

        private async void RefreshBag()
        {
            if (!SettingService.Account.Logined)
            {
                Notify.ShowMessageToast("请先登录");
                return;
            }
            await LoadBag();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// 读取直播头衔
        /// </summary>
        /// <returns></returns>
        public async Task GetTitles()
        {
            try
            {
                if (Titles != null)
                {
                    return;
                }
                var results = await m_liveRoomApi.LiveTitles().Request();
                if (!results.status)
                {
                    return;
                }

                var data = await results.GetData<List<LiveTitleModel>>();
                if (data.success)
                {
                    Titles = data.data;
                }
            }
            catch (Exception ex)
            {
                _logger.Log("读取直播头衔失败", LogType.Fatal, ex);
            }
        }

        /// <summary>
        /// 读取直播播放地址
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="qn"></param>
        /// <returns></returns>
        public async Task GetPlayUrl(int roomId, int qn = 0)
        {
            try
            {
                Loading = true;
                var results = await m_playerApi.LivePlayUrl(roomId.ToString(), qn).Request();
                if (!results.status)
                {
                    throw new CustomizedErrorException(results.message);
                }
                var data = await results.GetJson<ApiDataModel<LiveRoomPlayUrlModel>>();

                if (!data.success)
                {
                    throw new CustomizedErrorException(data.message);
                }
                // 暂时不优先使用flv流
                LiveRoomWebUrlStreamItemModel stream = null;
                if (data.data.PlayUrlInfo.PlayUrl.Stream.Any(item => item.ProtocolName == "http_hls"))
                {
                    stream = data.data.PlayUrlInfo.PlayUrl.Stream.FirstOrDefault(item => item.ProtocolName == "http_hls");
                }
                else if (data.data.PlayUrlInfo.PlayUrl.Stream.Any(item => item.ProtocolName == "http_stream"))
                {
                    stream = data.data.PlayUrlInfo.PlayUrl.Stream.FirstOrDefault(item => item.ProtocolName == "http_stream");
                }
                else
                {
                    throw new CustomizedErrorException("找不到直播流地址");
                }
                var codecList = stream.Format[0].Codec;

                var routeIndex = 1;
                foreach (var item in codecList.SelectMany(codecItem => codecItem.UrlInfo))
                {
                    item.Name = "线路" + routeIndex;
                    routeIndex++;
                }

                // 暂时不使用hevc流
                var codec = codecList.FirstOrDefault(item => item.CodecName == "avc");

                var acceptQnList = codec.AcceptQn;
                Qualites ??= data.data.PlayUrlInfo.PlayUrl.GQnDesc.Where(item => acceptQnList.Contains(item.Qn)).ToList();
                CurrentQn = data.data.PlayUrlInfo.PlayUrl.GQnDesc.FirstOrDefault(x => x.Qn == codec.CurrentQn);

                var urlList = codec.UrlInfo.Select(urlInfo => new LiveRoomRealPlayUrlsModel { Url = urlInfo.Host + codec.BaseUrl + urlInfo.Extra, Name = urlInfo.Name }).ToList();

                Urls = urlList;

                ChangedPlayUrl?.Invoke(this, data.data);
            }
            catch (Exception ex)
            {
                Notify.ShowMessageToast($"读取播放地址失败:{ex.Message}");
                _logger.Error($"读取播放地址失败:{ex.Message}", ex);
            }
            finally
            {
                Loading = false;
            }
        }

        /// <summary>
        /// 读取直播间详细信息
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task LoadLiveRoomDetail(string id)
        {
            try
            {
                if (m_cancelSource != null)
                {
                    m_cancelSource.Cancel();
                    m_cancelSource.Dispose();
                }
                if (m_liveMessage != null)
                {
                    m_liveMessage.NewMessage -= LiveMessage_NewMessage;
                    m_liveMessage = null;

                }
                m_liveMessage = new LiveMessage();
                m_liveMessage.NewMessage += LiveMessage_NewMessage;
                m_cancelSource = new System.Threading.CancellationTokenSource();

                Loading = true;
                var result = await m_liveRoomApi.LiveRoomInfo(id).Request();
                if (!result.status)
                {
                    throw new CustomizedErrorException(result.message);
                }

                var data = await result.GetData<LiveInfoModel>();
                if (!data.success)
                {
                    throw new CustomizedErrorException("加载直播间失败:" + data.message);
                }

                RoomID = data.data.RoomInfo.RoomId;
                RoomTitle = data.data.RoomInfo.Title;
                Online = data.data.RoomInfo.Online;
                Liveing = data.data.RoomInfo.LiveStatus == 1;
                LiveInfo = data.data;
                if (Ranks == null)
                {
                    Ranks = new List<LiveRoomRankViewModel>()
                    {
                        new LiveRoomRankViewModel(RoomID, data.data.RoomInfo.Uid, "金瓜子榜", "gold-rank"),
                        new LiveRoomRankViewModel(RoomID, data.data.RoomInfo.Uid, "今日礼物榜", "today-rank"),
                        new LiveRoomRankViewModel(RoomID, data.data.RoomInfo.Uid, "七日礼物榜", "seven-rank"),
                        new LiveRoomRankViewModel(RoomID, data.data.RoomInfo.Uid, "粉丝榜", "fans"),
                    };
                    SelectRank = Ranks[0];
                }


                await LoadAnchorProfile();
                if (Liveing)
                {
                    m_timer.Start();
                    await GetPlayUrl(RoomID,
                        SettingService.GetValue(SettingConstants.Live.DEFAULT_QUALITY, 10000));
                    //GetFreeSilverTime();  
                    await LoadSuperChat();
                    if (ReceiveLotteryMsg)
                    {
                        AnchorLotteryViewModel.LoadLotteryInfo(RoomID).RunWithoutAwait();
                    }
                }

                await GetRoomGiftList();
                await LoadBag();
                await LoadWalletInfo();
                if (Titles == null)
                {
                    await GetTitles();
                }

                EntryRoom();
                ReceiveMessage(data.data.RoomInfo.RoomId);
            }
            catch (CustomizedErrorException ex)
            {
                Notify.ShowMessageToast(ex.Message);
                _logger.Error(ex.Message, ex);
            }
            catch (Exception ex)
            {
                var result = HandelError<object>(ex);
                Notify.ShowMessageToast(ex.Message);
            }
            finally
            {
                Loading = false;
            }
        }

        /// <summary>
        /// 读取醒目留言
        /// </summary>
        /// <returns></returns>
        public async Task LoadSuperChat()
        {
            try
            {
                var result = await m_liveRoomApi.RoomSuperChat(RoomID).Request();
                if (!result.status)
                {
                    throw new CustomizedErrorException(result.message);
                }

                var data = await result.GetData<JObject>();
                if (!data.success)
                {
                    throw new CustomizedErrorException("读取醒目留言失败:" + data.message);
                }

                SuperChats.Clear();
                var ls = JsonConvert.DeserializeObject<List<LiveRoomSuperChatModel>>(
                    data.data["list"]?.ToString() ?? "[]");
                foreach (var item in ls)
                {
                    SuperChats.Add(new SuperChatMsgModel()
                    {
                        background_bottom_color = item.BackgroundBottomColor,
                        background_color = item.BackgroundColor,
                        background_image = item.BackgroundImage,
                        end_time = item.EndTime,
                        face = item.UserInfo.Face,
                        face_frame = item.UserInfo.FaceFrame,
                        font_color = string.IsNullOrEmpty(item.FontColor) ? "#FFFFFF" : item.FontColor,
                        max_time = item.EndTime - item.StartTime,
                        message = item.Message,
                        price = item.Price,
                        start_time = item.StartTime,
                        time = item.Time,
                        username = item.UserInfo.Uname
                    });
                }
            }
            catch (CustomizedErrorException ex)
            {
                Notify.ShowMessageToast(ex.Message);
                _logger.Error(ex.Message, ex);
            }
            catch (Exception ex)
            {
                var result = HandelError<object>(ex);
                Notify.ShowMessageToast(ex.Message);
            }
        }

        /// <summary>
        /// 进入房间
        /// </summary>
        public async void EntryRoom()
        {
            try
            {
                if (SettingService.Account.Logined)
                {
                    await m_liveRoomApi.RoomEntryAction(RoomID).Request();
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message, ex);
            }

        }

        /// <summary>
        /// 读取钱包信息
        /// </summary>
        /// <returns></returns>
        public async Task LoadWalletInfo()
        {
            try
            {
                if (!Logined)
                {
                    return;
                }
                var result = await m_liveRoomApi.MyWallet().Request();
                if (!result.status)
                {
                    throw new CustomizedErrorException(result.message);
                }

                var data = await result.GetData<LiveWalletInfo>();
                if (!data.success)
                {
                    throw new CustomizedErrorException("读取钱包失败:" + data.message);
                }

                WalletInfo = data.data;
            }
            catch (CustomizedErrorException ex)
            {
                Notify.ShowMessageToast(ex.Message);
                _logger.Error(ex.Message, ex);
            }
            catch (Exception ex)
            {
                var result = HandelError<object>(ex);
                Notify.ShowMessageToast(ex.Message);
            }
        }

        /// <summary>
        /// 读取背包礼物
        /// </summary>
        /// <returns></returns>
        public async Task LoadBag()
        {
            try
            {
                if (!Logined)
                {
                    return;
                }
                var result = await m_liveRoomApi.GiftList(LiveInfo.RoomInfo.AreaId, LiveInfo.RoomInfo.ParentAreaId, RoomID).Request();
                if (!result.status) return;

                var data = await result.GetData<JObject>();
                if (!data.success) return;
                var list = JsonConvert.DeserializeObject<List<LiveGiftItem>>(data.data["list"].ToString());

                var bagResult = await m_liveRoomApi.BagList(RoomID).Request();
                if (!bagResult.status)
                {
                    throw new CustomizedErrorException(bagResult.message);
                }

                var bagData = await bagResult.GetData<JObject>();
                if (!bagData.success)
                {
                    throw new CustomizedErrorException("读取背包失败:" + bagData.message);
                }

                BagGifts.Clear();
                var ls = JsonConvert.DeserializeObject<List<LiveBagGiftItem>>(
                    bagData.data["list"]?.ToString() ?? "[]");
                if (ls != null)
                    foreach (var item in ls)
                    {
                        var _gift = list.FirstOrDefault(x => x.Id == item.GiftId);
                        var gift = _gift.ObjectClone();
                        gift.GiftNum = item.GiftNum;
                        gift.CornerMark = item.CornerMark;
                        gift.BagId = item.BagId;
                        BagGifts.Add(gift);
                    }
                //WalletInfo = data.data;
            }
            catch (CustomizedErrorException ex)
            {
                Notify.ShowMessageToast(ex.Message);
                _logger.Error(ex.Message, ex);
            }
            catch (Exception ex)
            {
                var result = HandelError<object>(ex);
                Notify.ShowMessageToast(ex.Message);
            }
        }

        /// <summary>
        /// 读取主播资料
        /// </summary>
        /// <returns></returns>
        public async Task LoadAnchorProfile()
        {
            try
            {
                var result = await m_liveRoomApi.AnchorProfile(LiveInfo.RoomInfo.Uid).Request();
                if (!result.status)
                {
                    throw new CustomizedErrorException(result.message);
                }

                var data = await result.GetData<LiveAnchorProfile>();
                if (!data.success)
                {
                    throw new CustomizedErrorException("读取主播信息失败:" + data.message);
                }

                Profile = data.data;
                Attention = Profile.RelationStatus > 1;
            }
            catch (CustomizedErrorException ex)
            {
                Notify.ShowMessageToast(ex.Message);
                _logger.Error(ex.Message, ex);
            }
            catch (Exception ex)
            {
                var result = HandelError<object>(ex);
                Notify.ShowMessageToast(ex.Message);
            }
        }

        /// <summary>
        /// 读取直播间可用礼物列表
        /// </summary>
        /// <returns></returns>
        public async Task GetRoomGiftList()
        {
            try
            {
                var result = await m_liveRoomApi.GiftList(LiveInfo.RoomInfo.AreaId, LiveInfo.RoomInfo.ParentAreaId, RoomID).Request();
                if (!result.status) return;
                var data = await result.GetData<JObject>();
                if (!data.success) return;
                var list = JsonConvert.DeserializeObject<List<LiveGiftItem>>(data.data["list"].ToString());
                if (m_allGifts == null || m_allGifts.Count == 0)
                {
                    m_allGifts = list;
                }

                var resultRoom = await m_liveRoomApi
                    .RoomGifts(LiveInfo.RoomInfo.AreaId, LiveInfo.RoomInfo.ParentAreaId, RoomID).Request();
                if (!resultRoom.status) return;
                var dataRoom = await resultRoom.GetData<JObject>();
                var listRoom =
                    JsonConvert.DeserializeObject<List<LiveRoomGiftItem>>(dataRoom.data["list"]
                        .ToString());
                var liveGiftItems = new List<LiveGiftItem>()
                {
                    list.FirstOrDefault(x => x.Id == 1)
                };
                liveGiftItems.AddRange(listRoom.Select(item => list.FirstOrDefault(x => x.Id == item.GiftId)));

                Gifts = liveGiftItems;
            }
            catch (Exception ex)
            {
                Notify.ShowMessageToast("读取礼物信息失败");
                _logger.Log("读取礼物信息失败", LogType.Error, ex);
            }
        }

        /// <summary>
        /// 读取舰队信息
        /// </summary>
        /// <returns></returns>
        public async Task GetGuardList()
        {
            try
            {
                LoadingGuard = true;
                LoadMoreGuard = false;
                var result = await m_liveRoomApi.GuardList(LiveInfo.RoomInfo.Uid, RoomID, GuardPage).Request();
                if (!result.status) return;
                var data = await result.GetData<JObject>();
                if (!data.success) return;
                var top3 = JsonConvert.DeserializeObject<List<LiveGuardRankItem>>(data.data["top3"].ToString());
                if (Guards.Count == 0 && top3 != null && top3.Count != 0)
                {
                    foreach (var item in top3)
                    {
                        Guards.Add(item);
                    }
                }

                var list = JsonConvert.DeserializeObject<List<LiveGuardRankItem>>(data.data["list"].ToString());
                if (list != null && list.Count != 0)
                {
                    foreach (var item in list)
                    {
                        Guards.Add(item);
                    }
                }

                if (GuardPage >= data.data["info"]["page"].ToInt32())
                {
                    LoadMoreGuard = false;
                }
                else
                {
                    LoadMoreGuard = true;
                    GuardPage++;
                }
            }
            catch (Exception ex)
            {
                Notify.ShowMessageToast("读取舰队失败");
                _logger.Log("读取舰队失败", LogType.Error, ex);
            }
            finally
            {
                LoadingGuard = false;
            }
        }

        /// <summary>
        /// 加载更多舰队信息
        /// </summary>
        public async void LoadMoreGuardList()
        {
            if (LoadingGuard)
            {
                return;
            }
            await GetGuardList();
        }

        public async Task GetFreeSilverTime()
        {
            try
            {
                if (!SettingService.Account.Logined)
                {
                    ShowBox = false;
                    OpenBox = false;
                    return;
                }
                OpenBox = false;
                var result = await m_liveRoomApi.FreeSilverTime().Request();
                if (!result.status) return;
                var data = await result.GetData<JObject>();
                if (!data.success) return;
                ShowBox = true;
                freeSilverTime =
                    TimeExtensions.TimestampToDatetime(Convert.ToInt64(data.data["time_end"].ToString()));
                m_timerBox.Start();
            }
            catch (Exception ex)
            {
                Notify.ShowMessageToast("读取直播免费瓜子时间失败");
                _logger.Log("读取直播免费瓜子时间失败", LogType.Error, ex);
            }
        }

        public async Task GetFreeSilver()
        {
            try
            {
                if (!SettingService.Account.Logined)
                {
                    return;
                }
                var result = await m_liveRoomApi.GetFreeSilver().Request();
                if (!result.status) return;
                var data = await result.GetData<JObject>();
                if (data.success)
                {
                    Notify.ShowMessageToast("宝箱领取成功,瓜子+" + data.data["awardSilver"]);
                    //GetFreeSilverTime();
                    await LoadWalletInfo();
                }
                else
                {
                    await GetFreeSilverTime();
                }
            }
            catch (Exception ex)
            {
                Notify.ShowMessageToast("读取直播免费瓜子时间失败");
                _logger.Log("读取直播免费瓜子时间失败", LogType.Error, ex);
            }
        }

        public async Task SendGift(LiveGiftItem liveGiftItem)
        {
            if (!SettingService.Account.Logined && !await Notify.ShowLoginDialog())
            {
                Notify.ShowMessageToast("请先登录");
                return;
            }
            try
            {
                var result = await m_liveRoomApi.SendGift(LiveInfo.RoomInfo.Uid, liveGiftItem.Id, liveGiftItem.Num, RoomID, liveGiftItem.CoinType, liveGiftItem.Price).Request();
                if (!result.status)
                {
                    throw new CustomizedErrorException(result.message);
                }

                var data = await result.GetData<object>();
                if (!data.success)
                {
                    throw new CustomizedErrorException(data.message);
                }

                await LoadWalletInfo();
            }
            catch (CustomizedErrorException ex)
            {
                Notify.ShowMessageToast(ex.Message);
                _logger.Error(ex.Message, ex);
            }
            catch (Exception ex)
            {
                _logger.Log("赠送礼物出现错误", LogType.Error, ex);
                Notify.ShowMessageToast("赠送礼物出现错误");
            }

        }

        public async Task SendBagGift(LiveGiftItem liveGiftItem)
        {
            if (!SettingService.Account.Logined && !await Notify.ShowLoginDialog())
            {
                Notify.ShowMessageToast("请先登录");
                return;
            }
            try
            {
                var result = await m_liveRoomApi.SendBagGift(LiveInfo.RoomInfo.Uid, liveGiftItem.Id, liveGiftItem.Num, liveGiftItem.BagId, RoomID).Request();
                if (result.status)
                {
                    var data = await result.GetData<object>();
                    if (data.success)
                    {
                        await LoadBag();
                    }
                    else
                    {
                        Notify.ShowMessageToast(data.message);
                    }
                }
                else
                {
                    Notify.ShowMessageToast(result.message);
                }
            }
            catch (Exception ex)
            {
                _logger.Log("赠送礼物出现错误", LogType.Error, ex);
                Notify.ShowMessageToast("赠送礼物出现错误");
            }

        }

        public async Task<bool> SendDanmu(string text)
        {
            if (!SettingService.Account.Logined && !await Notify.ShowLoginDialog())
            {
                Notify.ShowMessageToast("请先登录");
                return false;
            }
            try
            {
                var result = await m_liveRoomApi.SendDanmu(text, RoomID).Request();
                if (!result.status)
                {
                    throw new CustomizedErrorException(result.message);
                }

                var data = await result.GetData<object>();
                if (!data.success)
                {
                    throw new CustomizedErrorException(data.message);
                }

                return true;
            }
            catch (CustomizedErrorException ex)
            {
                Notify.ShowMessageToast(ex.Message);
                _logger.Error(ex.Message, ex);
                return false;
            }
            catch (Exception ex)
            {
                _logger.Log("发送弹幕出现错误", LogType.Error, ex);
                Notify.ShowMessageToast("发送弹幕出现错误");
                return false;
            }
        }

        public void Dispose()
        {
            m_cancelSource?.Cancel();
            m_liveMessage?.Dispose();

            m_timer?.Stop();
            m_timerBox?.Stop();
            m_timerAutoHideGift?.Stop();
            if (AnchorLotteryViewModel != null)
            {
                AnchorLotteryViewModel.Timer.Stop();
                AnchorLotteryViewModel = null;
            }

            Messages?.Clear();
            Messages = null;
            GiftMessage?.Clear();
            GiftMessage = null;
            Guards?.Clear();
            Guards = null;
        }

        public void EmitSelectRankUpdate()
        {
            Set(nameof(SelectRank));
        }

        //public void SetDelay(int ms)
        //{
        //    if (liveDanmaku != null)
        //    {
        //        liveDanmaku.delay = ms;
        //    }
        //}

        #endregion
    }
}