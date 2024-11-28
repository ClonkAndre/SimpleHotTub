using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Linq;

using Newtonsoft.Json;
using CCL.GTAIV;

using SimpleHotTub.Classes;
using SimpleHotTub.Classes.Json;

using IVSDKDotNet;
using static IVSDKDotNet.Native.Natives;

namespace SimpleHotTub
{
    public class Main : Script
    {

        #region Variables
        public bool MenuOpen;

        private int playerHandle;
        private int playerGroupHandle;

        // Camera
        private CameraView currentCameraView;
        private NativeCamera hotTubCam;
        
        // Lists
        private List<HotTub> hotTubs;
        private List<PedOutfit> pedOutfits;
        private Dictionary<int, PedOutfitStorage> storedOutfits;
        private Dictionary<int, Vector3> storedPositions;

        // States
        private bool inHotTub;
        private bool wasCameraViewChanged;

        private bool canProcessEnteringSequence;
        private bool enteringSequenceStarted;

        private bool canProcessLeavingSequence;
        private bool leavingSequenceStarted;

        private bool waitingForMemberSpeechToFinish;
        private bool hasMemberReacted;

        // Other
        private HotTub currentHotTub;
        private Stopwatch timeInHotTubWatch;
        private bool allowVisualization;
        private bool visualizeHotTubStuff;
        private bool setNewSeat;
        private int forcedSeat = -1;
        #endregion

        #region Methods
        private void LoadHotTubs()
        {
            try
            {
                string path = string.Format("{0}\\HotTubs.json", ScriptResourceFolder);

                if (!File.Exists(path))
                {
                    Logging.LogError("Failed to load and add hot tubs. Details: The 'HotTubs.json' file was not found.");
                    return;
                }

                hotTubs.Clear();
                hotTubs = JsonConvert.DeserializeObject<List<HotTub>>(File.ReadAllText(path));

                Logging.Log("Loaded {0} hot tub(s).", hotTubs.Count);
            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to load and add hot tubs. Details: {0}", ex);
            }
        }
        private void LoadPedOutfits()
        {
            try
            {
                string path = string.Format("{0}\\PedOutfits.json", ScriptResourceFolder);

                if (!File.Exists(path))
                {
                    Logging.LogError("Failed to load and add ped outfits. Details: The 'PedOutfits.json' file was not found.");
                    return;
                }

                pedOutfits.Clear();
                pedOutfits = JsonConvert.DeserializeObject<List<PedOutfit>>(File.ReadAllText(path));

                Logging.Log("Loaded {0} ped outfit(s).", pedOutfits.Count);
            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to load and add ped outfits. Details: {0}", ex);
            }
        }
        private void SaveHotTubs()
        {
            try
            {
                string path = string.Format("{0}\\HotTubs.json", ScriptResourceFolder);

                File.WriteAllText(path, JsonConvert.SerializeObject(hotTubs, Formatting.Indented));

                Logging.Log("Saved {0} hot tub(s).", hotTubs.Count);
            }
            catch (Exception ex)
            {
                Logging.LogError("Failed to save hot tubs. Details: {0}", ex);
            }
        }

        private void ClearHelpMessages()
        {
            if (IS_THIS_HELP_MESSAGE_BEING_DISPLAYED("TM_1_4") || IS_THIS_HELP_MESSAGE_BEING_DISPLAYED("TM_1_5"))
            {
                CLEAR_HELP();
            }
        }

        private void EnterHotTub()
        {
            if (inHotTub)
                return;

            // Reset some things
            hasMemberReacted = false;
            waitingForMemberSpeechToFinish = false;
            currentCameraView = CameraView.Free;
            currentHotTub.Reset();

            // Create hot tub cam
            hotTubCam = NativeCamera.Create();
            hotTubCam.SetTargetPed(playerHandle);

            // Set default camera
            ChangeCameraView(true, (CameraView)ModSettings.DefaultHotTubCam);

            NativeGroup playerGroup = new NativeGroup(playerGroupHandle);

            // Get all peds from the group
            IVPed[] peds = playerGroup.ToArray(true);

            // Teleport them to the target seat and play anims
            for (int i = 0; i < peds.Length; i++)
            {
                IVPed ped = peds[i];
                int pedHandle = ped.GetHandle();
                
                GET_CHAR_MODEL(pedHandle, out uint pedModel);

                // Prepare the ped
                //SET_CHAR_COLLISION(pedHandle, false);
                //FREEZE_CHAR_POSITION(pedHandle, true);

                // Place them in their new clothes (If they got custom clothes)
                PedOutfit outfit = FindPedOutfit(pedModel);

                if (outfit != null)
                {
                    // Store current clothes
                    storedOutfits.Add(pedHandle, PedOutfitStorage.CreateFromPed(pedHandle));

                    // Set new clothes
                    SET_CHAR_COMPONENT_VARIATION(pedHandle, 1, outfit.UpperModel, outfit.UpperTexture);
                    SET_CHAR_COMPONENT_VARIATION(pedHandle, 2, outfit.LowerModel, outfit.LowerTexture);
                    SET_CHAR_COMPONENT_VARIATION(pedHandle, 5, outfit.FeetModel, outfit.FeetTexture);
                }

                // Store the current position
                storedPositions.Add(pedHandle, ped.Matrix.Pos);

                // Find free seat
                SeatInfo seat = currentHotTub.GetRandomUnoccupiedSeat();

                if (seat == null)
                {
                    Logging.LogDebug("Failed to find free seat for ped {0}!", pedHandle);
                    continue;
                }

                seat.SetOccupied(ped);

                // Teleport ped to found seat
                ped.Teleport(seat.Position, false, true);
                ped.SetHeading(seat.Heading);

                // Play animation
                if (!ped.PedFlags2.IsDruggedUp)
                {
                    if (IS_CHAR_MALE(pedHandle))
                        ped.GetAnimationController().Play("amb_sit_couch_m", "sit_down_idle_01", 3f, AnimationFlags.Loop);
                    else
                        ped.GetAnimationController().Play("amb_sit_couch_f", "sit_down_idle_01", 3f, AnimationFlags.Loop);
                }
                else
                {
                    // The "beggar_sit" animation is pretty low, so we temporarily increase the seat height
                    seat.TemporarilyRaiseHeight = true;
                    ped.GetAnimationController().Play("amb@beg_sitting", "beggar_sit", 3f, AnimationFlags.Loop);
                }
            }

            // Start time in hot tub stopwatch
            timeInHotTubWatch.Restart();

            inHotTub = true;
        }
        private void LeaveHotTub()
        {
            if (!inHotTub)
                return;

            // Delete cam
            if (hotTubCam.IsActive)
                hotTubCam.Deactivate();
            hotTubCam.Delete();
            hotTubCam = null;

            NativeGroup playerGroup = new NativeGroup(playerGroupHandle);

            // Get all peds from the group
            IVPed[] peds = playerGroup.ToArray(true);

            // Stop their anims and restore things
            for (int i = 0; i < peds.Length; i++)
            {
                IVPed ped = peds[i];
                int pedHandle = ped.GetHandle();

                // Restore some things
                //SET_CHAR_COLLISION(pedHandle, true);
                //FREEZE_CHAR_POSITION(pedHandle, false);

                // Stop animation
                ped.GetTaskController().ClearAllImmediately();

                // Restore previous clothes
                if (storedOutfits.TryGetValue(pedHandle, out PedOutfitStorage outfit))
                {
                    SET_CHAR_COMPONENT_VARIATION(pedHandle, 1, outfit.UpperModel, outfit.UpperTexture);
                    SET_CHAR_COMPONENT_VARIATION(pedHandle, 2, outfit.LowerModel, outfit.LowerTexture);
                    SET_CHAR_COMPONENT_VARIATION(pedHandle, 5, outfit.FeetModel, outfit.FeetTexture);
                }

                // Teleport to previous position
                if (storedPositions.TryGetValue(pedHandle, out Vector3 previousPos))
                {
                    ped.Teleport(previousPos, false, true);
                }
            }

            // Clear stored list
            storedOutfits.Clear();
            storedPositions.Clear();

            // Stop watch
            timeInHotTubWatch.Stop();

            inHotTub = false;
        }

        private void ProcessEnteringSequence()
        {
            if (!canProcessEnteringSequence)
                return;

            if (!enteringSequenceStarted)
            {
                if (!IS_SCREEN_FADED_OUT() && !IS_SCREEN_FADING_OUT())
                    DO_SCREEN_FADE_OUT(3000);

                if (IS_SCREEN_FADED_OUT())
                    enteringSequenceStarted = true;
            }
            else
            {
                EnterHotTub();

                if (!IS_SCREEN_FADED_IN() && !IS_SCREEN_FADING_IN())
                    DO_SCREEN_FADE_IN(3000);

                if (IS_SCREEN_FADED_IN())
                {
                    NativeGame.DisplayCustomHelpMessage("Press ~PAD_BACK~ to change view. Press ~INPUT_PICKUP~ to leave the hot tub.");
                    canProcessEnteringSequence = false;
                    enteringSequenceStarted = false;
                }
            }
        }
        private void ProcessLeavingSequence()
        {
            if (!canProcessLeavingSequence)
                return;

            if (!leavingSequenceStarted)
            {
                if (HasPlayerGroupMembers())
                {
                    if (!hasMemberReacted)
                    {
                        ReactToLeavingHotTub();
                        hasMemberReacted = true;
                    }
                    
                    if (IsAnyMemberOfGroupTalking())
                    {
                        waitingForMemberSpeechToFinish = true;
                        return;
                    }
                    else
                    {
                        waitingForMemberSpeechToFinish = false;
                    }
                }

                if (!IS_SCREEN_FADED_OUT() && !IS_SCREEN_FADING_OUT())
                    DO_SCREEN_FADE_OUT(3000);

                if (IS_SCREEN_FADED_OUT())
                    leavingSequenceStarted = true;
            }
            else
            {
                LeaveHotTub();

                if (!IS_SCREEN_FADED_IN() && !IS_SCREEN_FADING_IN())
                    DO_SCREEN_FADE_IN(3000);

                if (IS_SCREEN_FADED_IN())
                {
                    canProcessLeavingSequence = false;
                    leavingSequenceStarted = false;
                }
            }
        }

        private void ProcessHotTubLogic()
        {
            if (!inHotTub)
                return;

            // Keep all peds at their target seat position
            for (int i = 0; i < currentHotTub.SeatInfo.Count; i++)
            {
                SeatInfo seat = currentHotTub.SeatInfo[i];

                if (!seat.IsOccupied)
                    continue;
                if (!seat.Occupant.Exists())
                    continue;

                Vector3 additionalHeight = Vector3.Zero;

                if (seat.TemporarilyRaiseHeight)
                    additionalHeight = new Vector3(0f, 0f, 0.350f);

                seat.Occupant.Teleport(currentHotTub.Position + seat.Position + additionalHeight, false, true);
                seat.Occupant.SetHeading(seat.Heading);
            }

            if (setNewSeat)
            {
                SeatInfo seat = currentHotTub.GetSeat(forcedSeat);

                if (seat != null)
                {
                    if (!seat.IsOccupied)
                    {
                        // Reset current player seat
                        currentHotTub.GetSeatOccupiedByPlayer().Reset();

                        // Set new seat
                        seat.SetOccupied(NativeWorld.GetPedInstanceFromHandle(playerHandle));
                    }
                }

                setNewSeat = false;
            }
        }

        private void ChangeCameraView(bool set, CameraView newCameraView = CameraView.Free)
        {
            if (wasCameraViewChanged)
                return;

            if (!set)
            {
                currentCameraView = (CameraView)(int)currentCameraView + 1;

                if (currentCameraView == CameraView.MAX)
                    currentCameraView = CameraView.Free;
            }
            else
            {
                currentCameraView = newCameraView;
            }

            switch (currentCameraView)
            {
                case CameraView.Free:

                    if (hotTubCam.IsActive)
                        hotTubCam.Deactivate();

                    break;
                case CameraView.FrontRightSeat:

                    hotTubCam.Position = new Vector3(-414.971f, 1453.447f, 39.444f);
                    hotTubCam.Rotation = new Vector3(-0.753f, 0.0f, 118.990f);
                    hotTubCam.Activate();

                    break;
                case CameraView.FrontLeftSeat:

                    hotTubCam.Position = new Vector3(-416.8f, 1449.6f, 39.872f);
                    hotTubCam.Rotation = new Vector3(-9.436f, 0.0f, 19.578f);
                    hotTubCam.Activate();

                    break;
                case CameraView.BackRightSeat:

                    hotTubCam.Position = new Vector3(-421.317f, 1453.216f, 39.352f);
                    hotTubCam.Rotation = new Vector3(1.511f, 0.0f, -114.006f);
                    hotTubCam.Activate();

                    break;
                case CameraView.BackLeftSeat:

                    hotTubCam.Position = new Vector3(-418.050f, 1455.160f, 39.858f);
                    hotTubCam.Rotation = new Vector3(-7.406f, 0.0f, -177.903f);
                    hotTubCam.Activate();

                    break;
                case CameraView.Cinematic:

                    currentHotTub.ResetCinematicInfo();

                    hotTubCam.Activate();

                    break;
            }

            wasCameraViewChanged = true;
        }
        private void SetCamCinematicProperties(CinematicInfo info)
        {
            hotTubCam.FOV = info.FOV;
            hotTubCam.Position = info.From;

            if (info.UseFixedRotation)
            {
                hotTubCam.Unpoint();
                hotTubCam.Rotation = info.FixedRotation;
            }
            else
            {
                hotTubCam.PointAtCoord(info.LookAt);
            }
        }
        private void ProcessCinematicCam()
        {
            if (!inHotTub)
                return;
            if (currentCameraView != CameraView.Cinematic)
                return;
            if (currentHotTub == null)
                return;
            if (currentHotTub.CinematicInfo.Count == 0)
                return;

            // Get random cinematic info object if there is none yet
            if (currentHotTub.CurrentCinematicInfo == null)
            {
                // Get random cinematic info
                currentHotTub.LastCinematicInfoIndex = GENERATE_RANDOM_INT_IN_RANGE(0, currentHotTub.CinematicInfo.Count - 1);
                currentHotTub.CurrentCinematicInfo = currentHotTub.CinematicInfo[currentHotTub.LastCinematicInfoIndex];

                SetCamCinematicProperties(currentHotTub.CurrentCinematicInfo);
            }

            // Process cam movement
            if (Vector3.Distance(hotTubCam.Position, currentHotTub.CurrentCinematicInfo.To) < 0.001f)
            {
                // Get next random cinematic info
                int nextCinematicInfoIndex = GENERATE_RANDOM_INT_IN_RANGE(0, currentHotTub.CinematicInfo.Count - 1);

                // Try to not get the last index again
                while (nextCinematicInfoIndex == currentHotTub.LastCinematicInfoIndex)
                {
                    nextCinematicInfoIndex = GENERATE_RANDOM_INT_IN_RANGE(0, currentHotTub.CinematicInfo.Count - 1);
                }

                currentHotTub.LastCinematicInfoIndex = nextCinematicInfoIndex;
                currentHotTub.CurrentCinematicInfo = currentHotTub.CinematicInfo[currentHotTub.LastCinematicInfoIndex];

                SetCamCinematicProperties(currentHotTub.CurrentCinematicInfo);
            }
            else
            {
                hotTubCam.Position = hotTubCam.Position.MoveTowards(currentHotTub.CurrentCinematicInfo.To, currentHotTub.CurrentCinematicInfo.Speed);
            }

            // Clear the room for the games viewport if the cinematic cam is not within an interior
            GET_GAME_VIEWPORT_ID(out int vp);
            if (!currentHotTub.CurrentCinematicInfo.IsWithinInterior)
            {
                CLEAR_ROOM_FOR_VIEWPORT(vp);
            }
            else
            {
                GET_KEY_FOR_VIEWPORT_IN_ROOM(vp, out int roomKey);
                SET_ROOM_FOR_VIEWPORT_BY_KEY(vp, (uint)roomKey);
            }
        }
        #endregion

        #region Functions
        private HotTub FindClosestHotTub(Vector3 position)
        {
            return hotTubs.Where(x => Vector3.Distance(x.Position, position) < 2.20f).FirstOrDefault();
        }
        private PedOutfit FindPedOutfit(uint model)
        {
            return pedOutfits.Where(x => RAGE.AtStringHash(x.ModelName) == model).FirstOrDefault();
        }

        private bool HasPlayerGroupMembers()
        {
            if (IVNetwork.IsNetworkSession())
                return false;

            GET_GROUP_SIZE(playerGroupHandle, out int startIndex, out int count);
            return count > 0;
        }
        private bool IsAnyMemberOfGroupTalking()
        {
            NativeGroup playerGroup = new NativeGroup(playerGroupHandle);
            return playerGroup.ToList(true).Where(x => IS_ANY_SPEECH_PLAYING(x.GetHandle())).Any();
        }
        private bool ReactToLeavingHotTub()
        {
            if (!inHotTub)
                return false;
            if (!HasPlayerGroupMembers())
                return false;

            NativeGroup playerGroup = new NativeGroup(playerGroupHandle);

            if (timeInHotTubWatch.Elapsed.TotalSeconds < 30.0d)
            {
                IVPed member = playerGroup.GetMember(GENERATE_RANDOM_INT_IN_RANGE(0, playerGroup.MemberCount));

                if (GENERATE_RANDOM_INT_IN_RANGE(0, 100) < 35)
                {
                    _TASK_LOOK_AT_CHAR(playerHandle, member.GetHandle(), 4500, 0);
                    member.SayAmbientSpeech("STRIP_LEAVE_DISAGREE");
                    return true;
                }
            }

            return false;
        }
        #endregion

        #region Constructor
        public Main()
        {
            hotTubs = new List<HotTub>();
            pedOutfits = new List<PedOutfit>();
            storedOutfits = new Dictionary<int, PedOutfitStorage>(4);
            storedPositions = new Dictionary<int, Vector3>(4);

            timeInHotTubWatch = new Stopwatch();

            // IV-SDK .NET stuff
            Uninitialize += Main_Uninitialize;
            Initialized += Main_Initialized;
            ScriptCommandReceived += Main_ScriptCommandReceived;
            OnImGuiRendering += Main_OnImGuiRendering;
            Tick += Main_Tick;
        }
        #endregion

        private void Main_Uninitialize(object sender, EventArgs e)
        {
            LeaveHotTub();

            currentHotTub = null;

            if (hotTubs != null)
            {
                hotTubs.Clear();
                hotTubs = null;
            }
            if (pedOutfits != null)
            {
                pedOutfits.Clear();
                pedOutfits = null;
            }
            if (storedOutfits != null)
            {
                storedOutfits.Clear();
                storedOutfits = null;
            }
            if (storedPositions != null)
            {
                storedPositions.Clear();
                storedPositions = null;
            }
            if (timeInHotTubWatch != null)
            {
                timeInHotTubWatch.Stop();
                timeInHotTubWatch = null;
            }
        }
        private void Main_Initialized(object sender, EventArgs e)
        {
            ModSettings.Load(Settings);
            LoadHotTubs();
            LoadPedOutfits();
        }

        private object Main_ScriptCommandReceived(Script fromScript, object[] args, string command)
        {
            switch (command)
            {
                case "GET_IS_IN_HOT_TUB": return inHotTub;
                case "GET_TOTAL_TIME_IN_HOT_TUB": return timeInHotTubWatch.ElapsedTicks;
            }

            return null;
        }

        private void Main_OnImGuiRendering(IntPtr devicePtr, ImGuiIV_DrawingContext ctx)
        {
            if (!MenuOpen)
                return;

            if (ImGuiIV.Begin("Simple Hot Tub Modification", ref MenuOpen))
            {
                if (ImGuiIV.BeginTabBar("SimpleHotTubModTabBar"))
                {

                    DebugTab();
                    HotTubsTab();
                    SettingsTab();

                    ImGuiIV.EndTabBar();
                }
            }
            ImGuiIV.End();
        }
        private void DebugTab()
        {
            if (ImGuiIV.BeginTabItem("Debug##SimpleHotTubModTabItem"))
            {
                ImGuiIV.SeparatorText("States");
                ImGuiIV.BeginDisabled();
                ImGuiIV.CheckBox("InHotTub", ref inHotTub);
                ImGuiIV.CheckBox("WasCameraViewChanged", ref wasCameraViewChanged);
                ImGuiIV.CheckBox("CanProcessEnteringSequence", ref canProcessEnteringSequence);
                ImGuiIV.CheckBox("EnteringSequenceStarted", ref enteringSequenceStarted);
                ImGuiIV.CheckBox("CanProcessLeavingSequence", ref canProcessLeavingSequence);
                ImGuiIV.CheckBox("LeavingSequenceStarted", ref leavingSequenceStarted);
                ImGuiIV.CheckBox("WaitingForMemberSpeechToFinish", ref waitingForMemberSpeechToFinish);
                ImGuiIV.CheckBox("HasMemberReacted", ref hasMemberReacted);
                ImGuiIV.EndDisabled();

                ImGuiIV.Spacing();
                ImGuiIV.SeparatorText("Visualization");
                ImGuiIV.CheckBox("Allow Visualization", ref allowVisualization);
                ImGuiIV.CheckBox("Visualize Hot Tub Stuff", ref visualizeHotTubStuff);

                ImGuiIV.Spacing();
                ImGuiIV.SeparatorText("Seat");
                ImGuiIV.CheckBox("SetNewSeat", ref setNewSeat);
                ImGuiIV.SliderInt("ForcedSeat", ref forcedSeat, -1, 8);

                ImGuiIV.Spacing();
                ImGuiIV.SeparatorText("Other");
                ImGuiIV.TextUnformatted("Seconds in Hot Tub: {0}", timeInHotTubWatch.Elapsed.TotalSeconds);

                ImGuiIV.EndTabItem();
            }
        }
        private void HotTubsTab()
        {
            if (ImGuiIV.BeginTabItem("Hot Tubs##SimpleHotTubModTabItem"))
            {
                if (ImGuiIV.Button("Reload Hot Tubs"))
                {
                    LoadHotTubs();
                }
                ImGuiIV.SameLine();
                if (ImGuiIV.Button("Save Hot Tubs"))
                {
                    SaveHotTubs();
                }

                ImGuiIV.Spacing();
                ImGuiIV.SeparatorText("Loaded Hot Tubs");

                if (hotTubs.Count == 0)
                {
                    ImGuiIV.TextUnformatted("There are no hot tubs loaded.");
                }
                else
                {
                    ImGuiIV.TextDisabled("There are {0} loaded hot tubs.", hotTubs.Count);
                    ImGuiIV.Spacing();

                    for (int i = 0; i < hotTubs.Count; i++)
                    {
                        HotTub hotTub = hotTubs[i];

                        if (ImGuiIV.CollapsingHeader(string.Format("Hot Tub #{0}##SimpleHotTubModHeader", i)))
                        {

                            ImGuiIV.SeparatorText("Control");

                            if (ImGuiIV.Button("Delete"))
                            {
                                hotTubs.RemoveAt(i);
                                i--;
                                continue;
                            }

                            ImGuiIV.Spacing();
                            ImGuiIV.SeparatorText("Details");

                            ImGuiIV.DragFloat3(string.Format("Position##SimpleHotTubModTree_{0}", i), ref hotTub.Position, 0.01f);

                            ImGuiIV.Spacing();
                            if (ImGuiIV.TreeNode(string.Format("Seats##SimpleHotTubModTreeNode_{0}", i)))
                            {
                                for (int s = 0; s < hotTub.SeatInfo.Count; s++)
                                {
                                    SeatInfo seatInfo = hotTub.SeatInfo[s];

                                    if (ImGuiIV.TreeNode(string.Format("Seat #{0}##SimpleHotTubModTree_{1}", s, i)))
                                    {
                                        ImGuiIV.SeparatorText("Control");

                                        if (ImGuiIV.Button("Delete"))
                                        {
                                            hotTub.SeatInfo.RemoveAt(s);
                                            s--;
                                            ImGuiIV.TreePop();
                                            continue;
                                        }

                                        ImGuiIV.Spacing();
                                        ImGuiIV.SeparatorText("Details");

                                        ImGuiIV.DragFloat3("Position", ref seatInfo.Position, 0.01f);
                                        ImGuiIV.DragFloat("Heading", ref seatInfo.Heading);

                                        ImGuiIV.TreePop();
                                    }
                                }

                                ImGuiIV.TreePop();
                            }
                            if (ImGuiIV.TreeNode(string.Format("Cinematic Points##SimpleHotTubModTreeNode_{0}", i)))
                            {
                                for (int s = 0; s < hotTub.CinematicInfo.Count; s++)
                                {
                                    CinematicInfo cinematicInfo = hotTub.CinematicInfo[s];

                                    if (ImGuiIV.TreeNode(string.Format("Point #{0}##SimpleHotTubModTree_{1}", s, i)))
                                    {
                                        ImGuiIV.SeparatorText("Control");

                                        if (ImGuiIV.Button("Delete"))
                                        {
                                            hotTub.CinematicInfo.RemoveAt(s);
                                            s--;
                                            ImGuiIV.TreePop();
                                            continue;
                                        }

                                        ImGuiIV.Spacing();
                                        ImGuiIV.SeparatorText("Debug");
                                        ImGuiIV.CheckBox("Visualize", ref cinematicInfo.Visualize);

                                        ImGuiIV.Spacing();
                                        ImGuiIV.SeparatorText("Details");
                                        ImGuiIV.CheckBox("IsWithinInterior", ref cinematicInfo.IsWithinInterior);
                                        ImGuiIV.CheckBox("UseFixedRotation", ref cinematicInfo.UseFixedRotation);

                                        ImGuiIV.Spacing();
                                        ImGuiIV.DragFloat3("From", ref cinematicInfo.From);
                                        ImGuiIV.DragFloat3("To", ref cinematicInfo.To);
                                        ImGuiIV.DragFloat3("LookAt", ref cinematicInfo.LookAt);
                                        ImGuiIV.DragFloat3("FixedRotation", ref cinematicInfo.FixedRotation);

                                        ImGuiIV.Spacing();
                                        ImGuiIV.DragFloat("FOV", ref cinematicInfo.FOV);
                                        ImGuiIV.DragFloat("Speed", ref cinematicInfo.Speed, 0.001f);

                                        ImGuiIV.TreePop();
                                    }
                                }

                                ImGuiIV.TreePop();
                            }
                        }
                    }
                }

                ImGuiIV.EndTabItem();
            }
        }
        private void SettingsTab()
        {
            if (ImGuiIV.BeginTabItem("Settings##SimpleHotTubModTabItem"))
            {
                if (ImGuiIV.Button("Reload Settings"))
                {
                    if (Settings.Load())
                    {
                        ModSettings.Load(Settings);
                        Logging.Log("Settings file of SimpleHotTub was reloaded!");
                    }
                    else
                    {
                        Logging.LogWarning("Could not reload the settings file of SimpleHotTub! File might not exists.");
                    }
                }

                ImGuiIV.Spacing();
                ImGuiIV.SeparatorText("The Settings");

                ImGuiIV.TextUnformatted("Camera");
                ImGuiIV.SliderInt("DefaultHotTubCam", ref ModSettings.DefaultHotTubCam, 0, 5);

                ImGuiIV.EndTabItem();
            }
        }

        private void Main_Tick(object sender, EventArgs e)
        {
            playerHandle = NativeGame.GetPlayerPedHandle();
            GET_CHAR_COORDINATES(playerHandle, out Vector3 playerPos);

            // Check if there is any hot tub near the player
            currentHotTub = FindClosestHotTub(playerPos);

            if (currentHotTub != null)
            {
                if (IS_PED_RAGDOLL(playerHandle))
                    return;

                // Get the player group
                GET_PLAYER_GROUP(CONVERT_INT_TO_PLAYERINDEX(GET_PLAYER_ID()), out playerGroupHandle);

                // Check if the group the player is in is too big
                GET_GROUP_SIZE(playerGroupHandle, out int startIndex, out int count);
                if (count > currentHotTub.SeatInfo.Count)
                {
                    if (!IS_THIS_HELP_MESSAGE_BEING_DISPLAYED("TM_1_4"))
                        NativeGame.DisplayCustomHelpMessage(string.Format("Only {0} people can enter this hot tub at once.", currentHotTub.SeatInfo.Count), true, true, "TM_1_4");
                }
                else
                {
                    // Only allow key presses if any sequence was not started yet
                    if (!canProcessEnteringSequence && !canProcessLeavingSequence && !waitingForMemberSpeechToFinish)
                    {
                        // Check if player pressed the action key
                        bool wasActionKeyPressed =      NativeControls.IsGameKeyPressed(0, GameKey.Action);
                        bool wasChangeViewKeyPressed =  IS_CONTROL_JUST_PRESSED(2, 0);

                        if (!inHotTub)
                        {
                            // Only show message when it isn't already being displayed
                            if (!IS_THIS_HELP_MESSAGE_BEING_DISPLAYED("TM_1_4"))
                                NativeGame.DisplayCustomHelpMessage("Press ~INPUT_PICKUP~ to enter the hot tub.", false, true, "TM_1_4");

                            // Load desired anims
                            if (!HAVE_ANIMS_LOADED("amb_sit_couch_m"))
                                REQUEST_ANIMS("amb_sit_couch_m");
                            if (!HAVE_ANIMS_LOADED("amb_sit_couch_f"))
                                REQUEST_ANIMS("amb_sit_couch_f");
                            if (!HAVE_ANIMS_LOADED("amb@beg_sitting"))
                                REQUEST_ANIMS("amb@beg_sitting");

                            if (wasActionKeyPressed)
                            {
                                // Enter hot tub
                                canProcessEnteringSequence = true;
                            }
                        }
                        else
                        {
                            if (wasActionKeyPressed)
                            {
                                // Leave hot tub
                                canProcessLeavingSequence = true;
                            }
                            else if (wasChangeViewKeyPressed)
                            {
                                ChangeCameraView(false);
                            }
                            else
                            {
                                wasCameraViewChanged = false;
                            }
                        }
                    }
                }
            }
            else
            {
                ClearHelpMessages();
            }

            // Process stuff
            ProcessEnteringSequence();
            ProcessLeavingSequence();
            ProcessCinematicCam();
            ProcessHotTubLogic();

            if (allowVisualization)
            {
                for (int i = 0; i < hotTubs.Count; i++)
                {
                    HotTub hotTub = hotTubs[i];

                    if (visualizeHotTubStuff)
                    {
                        DRAW_CHECKPOINT(hotTub.Position, 0.5f, System.Drawing.Color.Red);

                        for (int s = 0; s < hotTub.SeatInfo.Count; s++)
                        {
                            DRAW_CHECKPOINT(hotTub.Position + hotTub.SeatInfo[s].Position, 0.25f, System.Drawing.Color.Blue);
                        }
                    }

                    for (int v = 0; v < hotTub.CinematicInfo.Count; v++)
                    {
                        CinematicInfo cinematicInfo = hotTub.CinematicInfo[v];

                        if (!cinematicInfo.Visualize)
                            continue;

                        DRAW_CHECKPOINT(cinematicInfo.From, 0.5f, System.Drawing.Color.Red);
                        DRAW_CHECKPOINT(cinematicInfo.To, 0.5f, System.Drawing.Color.Blue);
                        DRAW_CHECKPOINT(cinematicInfo.LookAt, 0.35f, System.Drawing.Color.Yellow);
                    }
                }
            }
        }

    }
}
