using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Uninarok.Networking.Client;
using Uninarok.Networking.Core.Transport;
using Uninarok.Networking.Data;
using Uninarok.Networking.Flow;
using Uninarok.Networking.Managers;
using Uninarok.Networking.Packets;
using Uninarok.Networking.Packets.In.Character;
using Uninarok.Networking.Packets.In.Login;
using Uninarok.Networking.Packets.In.Map;

namespace Uninarok.Networking.Tests
{
    // SPEC-0001 FR-10, FR-11, FR-13, FR-20; plan 4.9 (transition table, deadlines, stale events).
    public class FlowTransitionTests
    {
        private FakeNetworkClient _client;
        private FakeClock _clock;
        private NetworkSettings _settings;
        private NetworkFlow _flow;
        private readonly List<FlowState> _states = new List<FlowState>();

        private static readonly ServerEndpoint LoginServer = new ServerEndpoint("192.168.0.10", 6900, default, 0, 0, 0);

        [SetUp]
        public void SetUp()
        {
            _client = new FakeNetworkClient();
            _clock = new FakeClock { NowMs = 100_000 };
            _settings = new NetworkSettings();               // connect 15 s, reply 15 s, map load 60 s
            _flow = new NetworkFlow(_client, _clock, _settings, LoginServer);
            _states.Clear();
            _flow.StateChanged += s => _states.Add(s);
        }

        [TearDown]
        public void TearDown() => _flow.Dispose();

        // ---- drivers: bring the flow to a state through the real packets ----

        private void ToLoggingIn() => _flow.StartLogin("user", "pass");

        private void ToLoginConnected()
        {
            ToLoggingIn();
            _client.RaiseConnected(ConnectionRole.Login);
        }

        private void ToServerSelection()
        {
            ToLoginConnected();
            _client.Deliver(TestPackets.AcceptLoginWithOneServer("127.0.0.1", 6121));
        }

        private void ToConnectingToChar()
        {
            ToServerSelection();
            _flow.SelectServer(0);
        }

        private void ToCharSelection()
        {
            ToConnectingToChar();
            _client.RaiseConnected(ConnectionRole.Character);
            _client.Deliver(TestPackets.CharacterData(TestPackets.CharBytes(1, "One", 0)));
        }

        private void ToCreatingCharacter()
        {
            ToCharSelection();
            _flow.CreateCharacter(new CharacterDraft(new FixedString32Bytes("Hero"), 1, 3, 7, 4001, AccountSex.SEX_MALE));
        }

        private void ToSelectingCharacter()
        {
            ToCharSelection();
            _flow.SelectCharacter(0);
        }

        private void ToConnectingToMap(ushort port = 40000)
        {
            ToSelectingCharacter();
            _client.Deliver(TestPackets.MapAccepted(140593, "prontera", new byte[] { 10, 0, 0, 2 }, port));
        }

        private void ToLoadingMap()
        {
            ToConnectingToMap();
            _client.RaiseConnected(ConnectionRole.Map);
        }

        private void ToAuthenticated()
        {
            ToLoadingMap();
            _client.Deliver(TestPackets.AuthenticationAccepted());
        }

        private void ToInGame()
        {
            ToAuthenticated();
            _flow.MapLoaded();
        }

        private static readonly Regex Ignored = new Regex("ignored");

        // ---- happy path ----

        [Test]
        public void FullHappyPath_WalksEveryState_InOrder()
        {
            ToInGame();
            CollectionAssert.AreEqual(new[]
            {
                FlowState.LoggingIn, FlowState.ServerSelection, FlowState.ConnectingToChar, FlowState.CharSelection,
                FlowState.SelectingCharacter, FlowState.ConnectingToMap, FlowState.LoadingMap, FlowState.InGame,
            }, _states);
            Assert.IsNull(_flow.Failure);
        }

        [Test]
        public void ContextIsFilled_AlongTheWay()
        {
            ToAuthenticated();
            var ctx = _flow.Context;
            Assert.AreEqual(2000001u, ctx.AccountId);
            Assert.AreEqual(0x11223344, ctx.AuthCode);
            Assert.AreEqual(1, ctx.Servers.Count);
            Assert.AreEqual(1, ctx.Characters.Count);
            Assert.AreEqual(140593, ctx.CharacterId);
            Assert.AreEqual("10.0.0.2", ctx.Map.Host);
            Assert.AreEqual(40000, ctx.Map.Port);
            Assert.AreEqual(150, ctx.Entry.Position.X);
        }

        // ---- Idle -> LoggingIn -> ServerSelection ----

        [Test]
        public void StartLogin_FromIdle_ConnectsToTheLoginServer()
        {
            Assert.AreEqual(FlowState.Idle, _flow.State);
            ToLoggingIn();

            Assert.AreEqual(FlowState.LoggingIn, _flow.State);
            Assert.AreEqual(1, _client.Connects.Count);
            Assert.AreEqual("192.168.0.10", _client.Connects[0].Host);
            Assert.AreEqual(6900, _client.Connects[0].Port);
            Assert.AreEqual(ConnectionRole.Login, _client.Connects[0].Role);
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>(), "the login manager is active");
        }

        [Test]
        public void LoginConnected_SendsTheLoginRequest_Once()
        {
            ToLoginConnected();
            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_LOGIN));
            Assert.AreEqual(FlowState.LoggingIn, _flow.State);
        }

        [Test]
        public void LoginAccepted_DisconnectsAndMovesToServerSelection()
        {
            ToServerSelection();
            Assert.AreEqual(FlowState.ServerSelection, _flow.State);
            Assert.AreEqual(1, _client.DisconnectCalls, "the login connection is dropped");
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>(), "the login manager is left");
        }

        [Test]
        public void WrongCredentials_SendExactlyOneRequest_AndFailWithLoginRefused()
        {
            ToLoginConnected();
            _client.Deliver(TestPackets.Refusal(1));

            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.LoginRefused, _flow.Failure.Value.Code);
            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_LOGIN), "no automatic retry (FR-11)");
            Assert.AreEqual(1, _client.DisconnectCalls);
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<LOGIN_REFUSAL>());

            _clock.AdvanceSeconds(120);
            _flow.Tick();
            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_LOGIN), "still one request after time passes");
            Assert.AreEqual(1, _client.Connects.Count, "and no new connection");
        }

        [Test]
        public void EachRefusalPacket_FailsTheLogin()
        {
            foreach (var deliver in new Action[]
            {
                () => _client.Deliver(TestPackets.Refusal(6)),
                () => _client.Deliver(TestPackets.RefuseLogin3(0x0105)),
                () => _client.Deliver(TestPackets.ServerNotice(8)),
            })
            {
                SetUp();
                ToLoginConnected();
                deliver();
                Assert.AreEqual(FlowState.Failed, _flow.State);
                Assert.AreEqual(FailureCode.LoginRefused, _flow.Failure.Value.Code);
                StringAssert.Contains("code", _flow.Failure.Value.Message);
                _flow.Dispose();
            }
        }

        // ---- ServerSelection -> ConnectingToChar -> CharSelection ----

        [Test]
        public void SelectServer_ConnectsToTheChosenCharacterServer()
        {
            ToConnectingToChar();
            Assert.AreEqual(FlowState.ConnectingToChar, _flow.State);
            Assert.AreEqual("127.0.0.1", _client.Connects[1].Host);
            Assert.AreEqual(6121, _client.Connects[1].Port);
            Assert.AreEqual(ConnectionRole.Character, _client.Connects[1].Role);
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<CHARACTER_DATA>(), "the character manager is entered before the connection opens");
        }

        [Test]
        public void CharacterConnected_SkipsTheEchoBytes_ThenRequestsEntry()
        {
            ToConnectingToChar();
            _client.ClearRecords();
            _client.RaiseConnected(ConnectionRole.Character);
            CollectionAssert.AreEqual(new[] { "Skip 4", "Send 0x0065" }, _client.Actions);
        }

        [Test]
        public void CharactersReceived_MovesToCharSelection()
        {
            ToCharSelection();
            Assert.AreEqual(FlowState.CharSelection, _flow.State);
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(99)]
        public void SelectServer_WithAnInvalidIndex_IsIgnoredWithAWarning(int index)
        {
            ToServerSelection();
            LogAssert.Expect(LogType.Warning, Ignored);
            _flow.SelectServer(index);
            Assert.AreEqual(FlowState.ServerSelection, _flow.State);
            Assert.AreEqual(1, _client.Connects.Count, "no connection was opened");
        }

        // ---- CharSelection <-> Creating / Selecting ----

        [Test]
        public void CreateCharacter_SendsTheRequest_AndWaits()
        {
            ToCreatingCharacter();
            Assert.AreEqual(FlowState.CreatingCharacter, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.CREATE_CHARACTER));
        }

        [Test]
        public void CreationSuccess_ReturnsToCharSelection_WithTheNewCharacter()
        {
            ToCreatingCharacter();
            _client.Deliver(TestPackets.CreationSuccess(9, "Hero", 1));
            Assert.AreEqual(FlowState.CharSelection, _flow.State);
            Assert.AreEqual(2, _flow.Context.Characters.Count);
        }

        [Test]
        public void CreationFailure_ReturnsToCharSelection_AndKeepsTheReason()
        {
            ToCreatingCharacter();
            _client.Deliver(TestPackets.CreationFailed(CHARACTER_CREATION_FAILED.NameAlreadyExists));
            Assert.AreEqual(FlowState.CharSelection, _flow.State);
            Assert.AreEqual(0x00, _flow.Character.LastCreationFailure, "FR-13: the reason is exposed");
            Assert.IsNull(_flow.Failure, "a creation failure is not a flow failure");
        }

        [Test]
        public void SelectCharacter_SendsTheRequest_AndWaits()
        {
            ToSelectingCharacter();
            Assert.AreEqual(FlowState.SelectingCharacter, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.SELECT_CHARACTER));
            Assert.AreEqual(0, _flow.Context.SelectedSlot);
        }

        [Test]
        public void MapEnterRefused_ReturnsToCharSelection_AndKeepsTheReason()
        {
            ToSelectingCharacter();
            _client.Deliver(TestPackets.MapEnterRefused(0));
            Assert.AreEqual(FlowState.CharSelection, _flow.State);
            Assert.AreEqual(0, _flow.Character.LastMapEnterRefusal);
            Assert.IsNull(_flow.Failure);
        }

        // ---- SelectingCharacter -> ConnectingToMap -> LoadingMap -> InGame ----

        [Test]
        public void MapServerAssigned_LeavesTheCharacterServer_AndConnectsToTheMap()
        {
            ToConnectingToMap(40000);
            Assert.AreEqual(FlowState.ConnectingToMap, _flow.State);

            var connect = _client.Connects[_client.Connects.Count - 1];
            Assert.AreEqual("10.0.0.2", connect.Host);
            Assert.AreEqual(40000, connect.Port, "a map port above 32767 connects correctly (FR-12)");
            Assert.AreEqual(ConnectionRole.Map, connect.Role);

            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<CHARACTER_DATA>(), "the character manager is left");
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>(), "the zone manager is entered before the connection opens");
        }

        [Test]
        public void MapConnected_SendsTheEnterRequest_AndMovesToLoadingMap()
        {
            ToConnectingToMap();
            _client.ClearRecords();
            _client.RaiseConnected(ConnectionRole.Map);
            Assert.AreEqual(FlowState.LoadingMap, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_ENTER_MAP));
        }

        [Test]
        public void Authenticated_StaysInLoadingMap_AndFillsTheEntry()
        {
            ToAuthenticated();
            Assert.AreEqual(FlowState.LoadingMap, _flow.State);
            Assert.AreEqual(0x01020304u, _flow.Context.Entry.StartTick);
        }

        [Test]
        public void MapLoaded_AfterAuthentication_SendsMapLoaded_AndMovesInGame()
        {
            ToAuthenticated();
            _client.ClearRecords();
            _flow.MapLoaded();
            Assert.AreEqual(FlowState.InGame, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.MAP_LOADED));
        }

        [Test]
        public void MapLoaded_BeforeAuthentication_IsIgnoredWithAWarning()
        {
            ToLoadingMap();
            LogAssert.Expect(LogType.Warning, Ignored);
            _flow.MapLoaded();
            Assert.AreEqual(FlowState.LoadingMap, _flow.State);
            Assert.AreEqual(0, _client.SentCount(PacketIds.MAP_LOADED));
        }

        [Test]
        public void MapEntryRefusal_FailsWithMapRefused_AndLeavesTheZone()
        {
            ToLoadingMap();
            _client.Deliver(TestPackets.MapEntryRefusal(3));

            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.MapRefused, _flow.Failure.Value.Code);
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>());
            Assert.Greater(_client.DisconnectCalls, 0);
        }

        // ---- disconnects (CloseReason -> FailureCode) ----

        private static readonly (CloseReason Reason, FailureCode Code)[] Mapping =
        {
            (CloseReason.ConnectFailed, FailureCode.ConnectFailed),
            (CloseReason.RemoteClosed, FailureCode.RemoteClosed),
            (CloseReason.ProtocolFault, FailureCode.ProtocolFault),
            (CloseReason.Error, FailureCode.TransportError),
        };

        private static readonly (FlowState State, ConnectionRole Role)[] WaitingStates =
        {
            (FlowState.LoggingIn, ConnectionRole.Login),
            (FlowState.ConnectingToChar, ConnectionRole.Character),
            (FlowState.CharSelection, ConnectionRole.Character),
            (FlowState.CreatingCharacter, ConnectionRole.Character),
            (FlowState.SelectingCharacter, ConnectionRole.Character),
            (FlowState.ConnectingToMap, ConnectionRole.Map),
            (FlowState.LoadingMap, ConnectionRole.Map),
        };

        private void DriveTo(FlowState state)
        {
            switch (state)
            {
                case FlowState.LoggingIn: ToLoggingIn(); break;
                case FlowState.ServerSelection: ToServerSelection(); break;
                case FlowState.ConnectingToChar: ToConnectingToChar(); break;
                case FlowState.CharSelection: ToCharSelection(); break;
                case FlowState.CreatingCharacter: ToCreatingCharacter(); break;
                case FlowState.SelectingCharacter: ToSelectingCharacter(); break;
                case FlowState.ConnectingToMap: ToConnectingToMap(); break;
                case FlowState.LoadingMap: ToLoadingMap(); break;
                case FlowState.InGame: ToInGame(); break;
                default: throw new ArgumentOutOfRangeException(nameof(state));
            }
            Assert.AreEqual(state, _flow.State, "driver must reach " + state);
        }

        [Test]
        public void EveryCloseReason_InEveryWaitingState_FailsWithTheMappedCode()
        {
            foreach (var (state, role) in WaitingStates)
            foreach (var (reason, code) in Mapping)
            {
                SetUp();
                DriveTo(state);
                _client.RaiseDisconnected(new CloseInfo(reason, "closed"), role);

                Assert.AreEqual(FlowState.Failed, _flow.State, $"{state} + {reason}");
                Assert.AreEqual(code, _flow.Failure.Value.Code, $"{state} + {reason}");
                Assert.AreEqual(0, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>(), "managers are left");
                Assert.AreEqual(0, _client.Dispatcher.HandlerCount<CHARACTER_DATA>());
                Assert.AreEqual(0, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>());
                _flow.Dispose();
            }
        }

        [Test]
        public void InGame_MapDisconnect_MovesToDisconnected_NotFailed()
        {
            ToInGame();
            _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "server stopped"), ConnectionRole.Map);

            Assert.AreEqual(FlowState.Disconnected, _flow.State);
            Assert.IsNull(_flow.Failure);
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>());
        }

        [Test]
        public void DisconnectedOfAnInactiveRole_IsIgnored()
        {
            ToCharSelection();
            _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "stale login"), ConnectionRole.Login);
            _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "stale map"), ConnectionRole.Map);
            Assert.AreEqual(FlowState.CharSelection, _flow.State);
        }

        [Test]
        public void Disconnected_InServerSelection_IsIgnored_BecauseNoConnectionIsInUse()
        {
            ToServerSelection();
            _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "late"), ConnectionRole.Login);
            Assert.AreEqual(FlowState.ServerSelection, _flow.State);
        }

        [Test]
        public void ConnectedOfAnInactiveRole_IsIgnored()
        {
            ToLoggingIn();
            _client.ClearRecords();
            _client.RaiseConnected(ConnectionRole.Map);
            Assert.AreEqual(0, _client.Sent.Count);
            Assert.AreEqual(FlowState.LoggingIn, _flow.State);
        }

        // ---- deadlines (FR-10) ----

        [Test]
        public void LoggingIn_NeverConnected_TimesOutWithConnectTimeout()
        {
            ToLoggingIn();
            _clock.AdvanceSeconds(14.9);
            _flow.Tick();
            Assert.AreEqual(FlowState.LoggingIn, _flow.State);

            _clock.AdvanceSeconds(0.2);
            _flow.Tick();
            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.ConnectTimeout, _flow.Failure.Value.Code);
            Assert.AreEqual(1, _client.DisconnectCalls, "the pending connection is dropped");
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>());
        }

        [Test]
        public void LoggingIn_ConnectedButSilent_TimesOutWithReplyTimeout_MeasuredFromConnected()
        {
            ToLoggingIn();
            _clock.AdvanceSeconds(10);
            _client.RaiseConnected(ConnectionRole.Login);          // reply deadline now = connected time + 15 s
            _clock.AdvanceSeconds(14.9);
            _flow.Tick();
            Assert.AreEqual(FlowState.LoggingIn, _flow.State, "24.9 s after the start, but only 14.9 s after connecting");

            _clock.AdvanceSeconds(0.2);
            _flow.Tick();
            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.ReplyTimeout, _flow.Failure.Value.Code);
        }

        [Test]
        public void ConnectingToChar_TimesOut_BeforeAndAfterConnected()
        {
            ToConnectingToChar();
            _clock.AdvanceSeconds(15.1);
            _flow.Tick();
            Assert.AreEqual(FailureCode.ConnectTimeout, _flow.Failure.Value.Code);

            SetUp();
            ToConnectingToChar();
            _client.RaiseConnected(ConnectionRole.Character);
            _clock.AdvanceSeconds(15.1);
            _flow.Tick();
            Assert.AreEqual(FailureCode.ReplyTimeout, _flow.Failure.Value.Code);
        }

        [Test]
        public void CreatingCharacter_TimesOutWithReplyTimeout()
        {
            ToCreatingCharacter();
            _clock.AdvanceSeconds(14.9);
            _flow.Tick();
            Assert.AreEqual(FlowState.CreatingCharacter, _flow.State);
            _clock.AdvanceSeconds(0.2);
            _flow.Tick();
            Assert.AreEqual(FailureCode.ReplyTimeout, _flow.Failure.Value.Code);
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<CHARACTER_DATA>());
        }

        [Test]
        public void SelectingCharacter_TimesOutWithReplyTimeout()
        {
            ToSelectingCharacter();
            _clock.AdvanceSeconds(15.1);
            _flow.Tick();
            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.ReplyTimeout, _flow.Failure.Value.Code);
        }

        [Test]
        public void ConnectingToMap_NeverConnected_TimesOutWithConnectTimeout()
        {
            ToConnectingToMap();
            _clock.AdvanceSeconds(15.1);
            _flow.Tick();
            Assert.AreEqual(FailureCode.ConnectTimeout, _flow.Failure.Value.Code);
            Assert.AreEqual(0, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>());
        }

        [Test]
        public void LoadingMap_WaitingForTheServer_TimesOutWithReplyTimeout()
        {
            ToLoadingMap();
            _clock.AdvanceSeconds(15.1);
            _flow.Tick();
            Assert.AreEqual(FailureCode.ReplyTimeout, _flow.Failure.Value.Code);
        }

        [Test]
        public void LoadingMap_AfterAuthenticated_UsesTheMapLoadDeadline()
        {
            ToLoadingMap();
            _clock.AdvanceSeconds(5);
            _client.Deliver(TestPackets.AuthenticationAccepted());   // the deadline becomes now + MapLoadSeconds

            _clock.AdvanceSeconds(59.9);
            _flow.Tick();
            Assert.AreEqual(FlowState.LoadingMap, _flow.State, "the client's own scene load may take up to 60 s");

            _clock.AdvanceSeconds(0.2);
            _flow.Tick();
            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.ReplyTimeout, _flow.Failure.Value.Code);
        }

        [Test]
        public void StatesThatWaitForTheUser_HaveNoDeadline()
        {
            ToServerSelection();
            _clock.AdvanceSeconds(3600);
            _flow.Tick();
            Assert.AreEqual(FlowState.ServerSelection, _flow.State);

            SetUp();
            ToCharSelection();
            _clock.AdvanceSeconds(3600);
            _flow.Tick();
            Assert.AreEqual(FlowState.CharSelection, _flow.State);

            SetUp();
            ToInGame();
            _clock.AdvanceSeconds(3600);
            _flow.Tick();
            Assert.AreEqual(FlowState.InGame, _flow.State);
        }

        [Test]
        public void LeavingAWaitingState_ClearsItsDeadline()
        {
            ToLoginConnected();
            _clock.AdvanceSeconds(10);
            _client.Deliver(TestPackets.AcceptLoginWithOneServer());      // ServerSelection: no deadline
            _clock.AdvanceSeconds(30);
            _flow.Tick();
            Assert.AreEqual(FlowState.ServerSelection, _flow.State, "the old login deadline must not fire");
        }

        [Test]
        public void TimeoutsFollowTheSettings()
        {
            _settings.ConnectSeconds = 2f;
            ToLoggingIn();
            _clock.AdvanceSeconds(2.1);
            _flow.Tick();
            Assert.AreEqual(FailureCode.ConnectTimeout, _flow.Failure.Value.Code);
        }

        // ---- map change on the same map server (SPEC-0010 FR-15, PLAN-0010 4.8) ----

        private static MAP_CHANGE MapChange()
        {
            var b = new byte[MAP_CHANGE.Size - 2];
            System.Text.Encoding.ASCII.GetBytes("prt_fild08.gat").CopyTo(b, 0);
            b[16] = 170;
            b[18] = 0x7B; b[19] = 0x01;      // 379
            return new MAP_CHANGE(b);
        }

        [Test]
        public void NetworkFlow_MapChange_LoadingMapThenInGame()
        {
            ToInGame();
            _client.Status = ConnectionStatus.Connected;
            string host = _flow.Context.Map.Host;
            ushort port = _flow.Context.Map.Port;
            int requests = 0;
            FlowState seenIn = FlowState.Idle;
            _flow.MapLoadRequested += () => { requests++; seenIn = _flow.State; };
            _client.ClearRecords();

            _client.Deliver(MapChange());

            Assert.AreEqual(FlowState.LoadingMap, _flow.State);
            Assert.AreEqual((1, FlowState.LoadingMap), (requests, seenIn));
            Assert.AreEqual("prt_fild08.gat", _flow.Context.Map.MapName.ToString());
            Assert.AreEqual((host, port), (_flow.Context.Map.Host, _flow.Context.Map.Port), "the same server");
            Assert.AreEqual((170, 379), ((int)_flow.Context.Entry.Position.X, (int)_flow.Context.Entry.Position.Y));
            Assert.AreEqual(UninarokDirection.North, _flow.Context.Entry.Position.Direction, "PLAN-0010 D14");
            Assert.AreEqual(0, _client.DisconnectCalls);

            _clock.AdvanceSeconds(10.1);
            _flow.Tick();
            Assert.AreEqual(0, _client.SentCount(PacketIds.HEARTBEAT_REQUEST), "no heartbeat while the map loads");

            _flow.MapLoaded();
            Assert.AreEqual(FlowState.InGame, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.MAP_LOADED), "MAP_LOADED again");
            Assert.AreEqual(1, _client.SentCount(PacketIds.HEARTBEAT_REQUEST), "and the heartbeat with it");
            _clock.AdvanceSeconds(10_000);
            _flow.Tick();
            Assert.AreEqual(FlowState.InGame, _flow.State, "no deadline left");
        }

        [Test]
        public void MapChange_WhileLoading_AsksForTheNewMap_AndRestartsTheDeadline()
        {
            ToInGame();
            int requests = 0;
            _flow.MapLoadRequested += () => requests++;
            _client.Deliver(MapChange());
            _clock.AdvanceSeconds(_settings.MapLoadSeconds - 1);
            _client.Deliver(MapChange());
            Assert.AreEqual(2, requests);
            _clock.AdvanceSeconds(_settings.MapLoadSeconds - 1);
            _flow.Tick();
            Assert.AreEqual(FlowState.LoadingMap, _flow.State);
        }

        [Test]
        public void MapChange_LoadDeadline_AndDisconnect_FailTheFlow()
        {
            ToInGame();
            _client.Deliver(MapChange());
            _clock.AdvanceSeconds(_settings.MapLoadSeconds + 1);
            _flow.Tick();
            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.ReplyTimeout, _flow.Failure.Value.Code);

            SetUp();
            ToInGame();
            _client.Deliver(MapChange());
            _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "gone"), ConnectionRole.Map);
            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.RemoteClosed, _flow.Failure.Value.Code);
        }

        [Test]
        public void MapLoadRequested_OnFirstEntry_ComesWithTheAuthentication()
        {
            int requests = 0;
            SetUp();
            _flow.MapLoadRequested += () => requests++;
            ToInGame();
            Assert.AreEqual(1, requests);
        }

        private static MAP_CHANGE MapChange(string map, int x, int y)
        {
            var b = new byte[MAP_CHANGE.Size - 2];
            System.Text.Encoding.ASCII.GetBytes(map).CopyTo(b, 0);
            b[16] = (byte)x; b[17] = (byte)(x >> 8);
            b[18] = (byte)y; b[19] = (byte)(y >> 8);
            return new MAP_CHANGE(b);
        }

        [Test]
        public void MapChange_ToTheVeryEntry_RightAfterIt_IsDropped_Once([Values(false, true)] bool afterMapLoaded)
        {
            // Hercules pc_authok: the real captures hold it after every AUTHENTICATION_ACCEPTED (PLAN-0010 D23)
            int requests = 0;
            _flow.MapLoadRequested += () => requests++;
            ToAuthenticated();
            var entry = _flow.Context.Entry;
            string map = _flow.Context.Map.MapName.ToString();
            Assert.AreEqual(1, requests);
            if (afterMapLoaded) _flow.MapLoaded();
            var state = _flow.State;
            _client.Deliver(MapChange(map, entry.Position.X, entry.Position.Y));
            Assert.AreEqual((1, state), (requests, _flow.State), "nothing to load");
            Assert.AreEqual((entry.StartTick, entry.Position.Direction), (_flow.Context.Entry.StartTick, _flow.Context.Entry.Position.Direction));
            if (!afterMapLoaded) _flow.MapLoaded();

            _client.Deliver(MapChange(map, entry.Position.X, entry.Position.Y));
            Assert.AreEqual((2, FlowState.LoadingMap), (requests, _flow.State), "a second one is a warp to the same cell");
        }

        [Test]
        public void MapChange_Elsewhere_RightAfterTheEntry_IsAMapChange()
        {
            int requests = 0;
            _flow.MapLoadRequested += () => requests++;
            ToAuthenticated();
            _client.Deliver(MapChange("prt_fild08.gat", 1, 2));
            Assert.AreEqual(2, requests);
            Assert.AreEqual("prt_fild08.gat", _flow.Context.Map.MapName.ToString());
        }

        [Test]
        public void MapChange_AfterAMapServerChange_IsNeverTheEcho()
        {
            ToInGame();
            _client.Deliver(MapServerChange());
            _client.RaiseConnected();
            _client.Deliver(new AUTHENTICATION_ACCEPTED(new byte[] { 0x04, 0x03, 0x02, 0x01, 0x25, 0x8C, 0x84, 0x05, 0x05, 0x00, 0x00 }));
            _flow.MapLoaded();
            _client.Deliver(MapChange("prt_fild08.gat", 150, 200));
            Assert.AreEqual(FlowState.LoadingMap, _flow.State);
        }

        // ---- move to another map server (SPEC-0010 FR-15, PLAN-0010 4.8) ----

        private static MAP_SERVER_CHANGE MapServerChange()
        {
            var b = new byte[MAP_SERVER_CHANGE.Size - 2];
            System.Text.Encoding.ASCII.GetBytes("prt_fild08.gat").CopyTo(b, 0);
            b[16] = 170;
            b[18] = 0x7B; b[19] = 0x01;                                    // 379
            b[20] = 10; b[21] = 0; b[22] = 0; b[23] = 9;                   // 10.0.0.9
            b[24] = 0xC9; b[25] = 0xC3;                                    // 50121
            return new MAP_SERVER_CHANGE(b);
        }

        [Test]
        public void NetworkFlow_MapServerChange_Reconnects()
        {
            ToInGame();
            byte[] firstEntry = null;
            foreach (var s in _client.Sent)
                if (s.Id == PacketIds.REQUEST_ENTER_MAP) firstEntry = s.Bytes;
            int requests = 0;
            _flow.MapLoadRequested += () => requests++;
            _client.ClearRecords();

            _client.Deliver(MapServerChange());

            CollectionAssert.AreEqual(new[] { "Disconnect", "Connect Map 10.0.0.9:50121" }, _client.Actions, "the old connection is dropped silently, then the new one opens");
            Assert.AreEqual(FlowState.ConnectingToMap, _flow.State);
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<AUTHENTICATION_ACCEPTED>(), "the zone manager is entered again, once");

            _client.RaiseConnected();
            Assert.AreEqual(FlowState.LoadingMap, _flow.State);
            Assert.AreEqual(1, _client.SentCount(PacketIds.REQUEST_ENTER_MAP));
            var entry = _client.Sent[0].Bytes;
            for (int i = 0; i < entry.Length; i++)
                if (i < 18 || i > 21) Assert.AreEqual(firstEntry[i], entry[i], $"byte {i}: same account, character, auth code and sex (18-21 is the timestamp)");
            Assert.AreEqual(0, requests);

            _client.Deliver(new AUTHENTICATION_ACCEPTED(new byte[] { 0x04, 0x03, 0x02, 0x01, 0x25, 0x8C, 0x84, 0x05, 0x05, 0x00, 0x00 }));
            Assert.AreEqual(1, requests);
            Assert.AreEqual((150, 200), ((int)_flow.Context.Entry.Position.X, (int)_flow.Context.Entry.Position.Y), "from here the first-entry path");
            _flow.MapLoaded();
            Assert.AreEqual(FlowState.InGame, _flow.State);
            Assert.AreEqual("prt_fild08.gat", _flow.Context.Map.MapName.ToString());
            Assert.AreEqual(1, _client.SentCount(PacketIds.MAP_LOADED));
        }

        [Test]
        public void MapServerChange_ConnectTimeout_AndRefusal_FailTheFlow()
        {
            ToInGame();
            _client.Deliver(MapServerChange());
            _clock.AdvanceSeconds(_settings.ConnectSeconds + 1);
            _flow.Tick();
            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.ConnectTimeout, _flow.Failure.Value.Code);

            SetUp();
            ToInGame();
            _client.Deliver(MapServerChange());
            _client.RaiseConnected();
            _client.Deliver(new MAP_ENTRY_REFUSAL(new byte[] { 3 }));
            Assert.AreEqual(FlowState.Failed, _flow.State);
            Assert.AreEqual(FailureCode.MapRefused, _flow.Failure.Value.Code);
        }

        // ---- keepalives are driven by Tick (FR-14) ----

        [Test]
        public void Tick_DrivesTheCharacterPing_InCharSelection()
        {
            ToCharSelection();
            _client.ClearRecords();
            _client.Status = ConnectionStatus.Connected;
            _flow.Tick();                                    // schedules the first ping
            _clock.AdvanceSeconds(12.1);
            _flow.Tick();
            Assert.AreEqual(1, _client.SentCount(PacketIds.CHARACTER_SERVER_PING));
        }

        [Test]
        public void Tick_DrivesTheMapHeartbeat_InGame()
        {
            ToInGame();
            _client.ClearRecords();
            _client.Status = ConnectionStatus.Connected;
            _clock.AdvanceSeconds(10.1);
            _flow.Tick();
            Assert.AreEqual(1, _client.SentCount(PacketIds.HEARTBEAT_REQUEST));
        }

        [Test]
        public void NoKeepalives_InServerSelection_OrAfterTheMapConnectionDropped()
        {
            ToServerSelection();
            _client.ClearRecords();
            _clock.AdvanceSeconds(100);
            _flow.Tick();
            Assert.AreEqual(0, _client.Sent.Count);

            SetUp();
            ToInGame();
            _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "gone"), ConnectionRole.Map);
            _client.ClearRecords();
            _clock.AdvanceSeconds(100);
            _flow.Tick();
            Assert.AreEqual(0, _client.Sent.Count, "none after leaving that server");
        }

        // ---- commands in the wrong state are rejected with a warning ----

        private static readonly Dictionary<string, FlowState[]> ValidIn = new Dictionary<string, FlowState[]>
        {
            ["StartLogin"] = new[] { FlowState.Idle },
            ["SelectServer"] = new[] { FlowState.ServerSelection },
            ["CreateCharacter"] = new[] { FlowState.CharSelection },
            ["SelectCharacter"] = new[] { FlowState.CharSelection },
            ["MapLoaded"] = new[] { FlowState.LoadingMap },
            ["Reset"] = new[] { FlowState.Failed, FlowState.Disconnected },
        };

        private void Invoke(string command)
        {
            switch (command)
            {
                case "StartLogin": _flow.StartLogin("u", "p"); break;
                case "SelectServer": _flow.SelectServer(0); break;
                case "CreateCharacter": _flow.CreateCharacter(new CharacterDraft(new FixedString32Bytes("X"), 0, 0, 0, 0, AccountSex.SEX_MALE)); break;
                case "SelectCharacter": _flow.SelectCharacter(0); break;
                case "MapLoaded": _flow.MapLoaded(); break;
                case "Reset": _flow.Reset(); break;
            }
        }

        [Test]
        public void EveryCommand_InEveryWrongState_IsRejectedWithAWarning_AndChangesNothing()
        {
            var allStates = (FlowState[])Enum.GetValues(typeof(FlowState));
            foreach (var kv in ValidIn)
            foreach (var state in allStates)
            {
                if (Array.IndexOf(kv.Value, state) >= 0) continue;

                SetUp();
                ReachAny(state);
                if (_flow.State != state) { _flow.Dispose(); continue; }       // MapLoaded is valid only before authentication; see its own test

                int connects = _client.Connects.Count, sent = _client.Sent.Count;
                LogAssert.Expect(LogType.Warning, Ignored);
                Invoke(kv.Key);

                Assert.AreEqual(state, _flow.State, $"{kv.Key} in {state} must not change the state");
                Assert.AreEqual(connects, _client.Connects.Count, $"{kv.Key} in {state} opened a connection");
                Assert.AreEqual(sent, _client.Sent.Count, $"{kv.Key} in {state} sent a packet");
                _flow.Dispose();
            }
        }

        private void ReachAny(FlowState state)
        {
            switch (state)
            {
                case FlowState.Idle: break;
                case FlowState.Failed: ToLoggingIn(); _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "x"), ConnectionRole.Login); break;
                case FlowState.Disconnected: ToInGame(); _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "x"), ConnectionRole.Map); break;
                default: DriveTo(state); break;
            }
        }

        // ---- Reset ----

        [Test]
        public void Reset_FromFailed_ClearsTheFailureAndTheContext_AndAllowsANewLogin()
        {
            ToLoginConnected();
            _client.Deliver(TestPackets.AcceptLoginWithOneServer());
            _client.RaiseConnected(ConnectionRole.Login);
            _flow.SelectServer(0);
            _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "lost"), ConnectionRole.Character);
            Assert.AreEqual(FlowState.Failed, _flow.State);

            _flow.Reset();
            Assert.AreEqual(FlowState.Idle, _flow.State);
            Assert.IsNull(_flow.Failure);
            Assert.AreEqual(0u, _flow.Context.AccountId);
            Assert.AreEqual(0, _flow.Context.Servers.Count);

            _flow.StartLogin("user", "pass");
            Assert.AreEqual(FlowState.LoggingIn, _flow.State);
            Assert.AreEqual(1, _client.Dispatcher.HandlerCount<ACCEPT_LOGIN>(), "exactly one subscription after the retry");
        }

        [Test]
        public void Reset_FromDisconnected_ReturnsToIdle()
        {
            ReachAny(FlowState.Disconnected);
            _flow.Reset();
            Assert.AreEqual(FlowState.Idle, _flow.State);
        }

        [Test]
        public void StartLogin_AfterAFailure_NeedsAResetFirst()
        {
            ReachAny(FlowState.Failed);
            LogAssert.Expect(LogType.Warning, Ignored);
            _flow.StartLogin("user", "pass");
            Assert.AreEqual(FlowState.Failed, _flow.State);
        }

        // ---- StateChanged and Dispose ----

        [Test]
        public void StateChanged_IsRaisedOncePerChange_NotForNoOps()
        {
            ToServerSelection();
            _states.Clear();
            _client.RaiseConnected(ConnectionRole.Character);        // ignored: not the active role
            _flow.Tick();
            Assert.AreEqual(0, _states.Count);
        }

        [Test]
        public void Dispose_UnsubscribesFromTheClientAndTheManagers()
        {
            ToLoggingIn();
            _flow.Dispose();

            _client.RaiseDisconnected(new CloseInfo(CloseReason.RemoteClosed, "late"), ConnectionRole.Login);
            _client.Deliver(TestPackets.Refusal(1));
            Assert.AreEqual(FlowState.LoggingIn, _flow.State, "a disposed flow no longer reacts");
        }

        [Test]
        public void StateChangedHandlerThatThrows_DoesNotBreakTheFlow()
        {
            _flow.StateChanged += s => throw new InvalidOperationException("ui bug");
            LogAssert.Expect(LogType.Exception, new Regex("ui bug"));
            ToLoggingIn();
            Assert.AreEqual(FlowState.LoggingIn, _flow.State);
        }

        [Test]
        public void Constructor_RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new NetworkFlow(null, _clock, _settings, LoginServer));
            Assert.Throws<ArgumentNullException>(() => new NetworkFlow(_client, null, _settings, LoginServer));
            Assert.Throws<ArgumentNullException>(() => new NetworkFlow(_client, _clock, null, LoginServer));
        }
    }
}
