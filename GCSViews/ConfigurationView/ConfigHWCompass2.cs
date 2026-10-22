using MissionPlanner.Controls;
using MissionPlanner.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Color = System.Drawing.Color;

namespace MissionPlanner.GCSViews.ConfigurationView
{
    public partial class ConfigHWCompass2 : MyUserControl, IActivate, IDeactivate
    {
        private List<CompassDeviceInfo> list;

        private BindingSource bs;

        private bool rebootrequired = false;

        private MagCalMonitor magcal = new MagCalMonitor();

        private Color picturedefault = Color.Empty;

        public class CompassDeviceInfo : DeviceInfo
        {
            private string _orient;
            private bool _external;

            public CompassDeviceInfo(int index, string ParamName, uint id) : base(index, ParamName, id)
            {
                //set initial state
                var id1 = MainV2.comPort.MAV.param[new[] { "COMPASS_DEV_ID", "COMPASS1_DEV_ID"}];
                var id2 = MainV2.comPort.MAV.param[new[] { "COMPASS_DEV_ID2", "COMPASS2_DEV_ID"}];
                var id3 = MainV2.comPort.MAV.param[new[] { "COMPASS_DEV_ID3", "COMPASS3_DEV_ID"}];

                var idO1 = MainV2.comPort.MAV.param[new[] { "COMPASS_ORIENT", "COMPASS1_ORIENT"}];
                var idO2 = MainV2.comPort.MAV.param[new[] { "COMPASS_ORIENT2", "COMPASS2_ORIENT"}];
                var idO3 = MainV2.comPort.MAV.param[new[] { "COMPASS_ORIENT3", "COMPASS3_ORIENT"}];

                var idE1 = MainV2.comPort.MAV.param[new[] { "COMPASS_EXTERNAL", "COMPASS1_EXTERN"}];
                var idE2 = MainV2.comPort.MAV.param[new[] { "COMPASS_EXTERN2", "COMPASS2_EXTERN"}];
                var idE3 = MainV2.comPort.MAV.param[new[] { "COMPASS_EXTERN3", "COMPASS3_EXTERN"}];

                if (id1 != null && id1?.Value == id)
                {
                    _orient = idO1?.ToString();
                    _external = idE1?.Value > 0 ? true : false;
                }
                if (id2 != null && id2?.Value == id)
                {
                    _orient = idO2?.ToString();
                    _external = idE2?.Value > 0 ? true : false;
                }
                if (id3 != null && id3?.Value == id)
                {
                    _orient = idO3?.ToString();
                    _external = idE3?.Value > 0 ? true : false;
                }
            }

            public string Orient
            {
                get => _orient;
                set => _orient = value;
            }

            public bool External
            {
                get => _external;
                set => _external = value;
            }

            public bool Missing
            {
                get;
                set;
            }
        }

        public ConfigHWCompass2()
        {
            InitializeComponent();
        }

        public void Activate()
        {
            // COMPASS_DEV_ID get a list of all connected devices
            list = MainV2.comPort.MAV.param.Where(a => a.Name.StartsWith("COMPASS") && a.Name.Contains("DEV_ID") && a.Value != 0)
                .Select((a, b) => new CompassDeviceInfo(b, a.Name, (uint) a.Value))
                .OrderBy((a) => a.ParamName).ToList();

            // COMPASS_PRIO get a list of all prios
            var prio = MainV2.comPort.MAV.param.Where(a => a.Name.StartsWith("COMPASS_PRIO") && a.Value != 0)
                .Select((a, b) => new CompassDeviceInfo(b, a.Name, (uint)a.Value))
                .OrderBy((a) => a.ParamName).ToList();

            var anymissing = false;
            // mark missing
            prio.ForEach(a =>
            {
                if (a.DevID == 0 || list.Any(b => b.DevID == a.DevID))
                    a.Missing = false;
                else
                {
                    a.Missing = true;
                    anymissing = true;
                }
            });

            //filter list removing prio dups from the list
            list = list.Where(a => !prio.Any(b => b.DevID == a.DevID)).ToList();

            // insert prios at the top
            list.InsertRange(0, prio);

            bs = new BindingSource();
            bs.DataSource = list;
            myDataGridView1.DataSource = bs;

            mavlinkComboBoxfitness.setup(ParameterMetaDataRepository.GetParameterOptionsInt("COMPASS_CAL_FIT",
                MainV2.comPort.MAV.cs.firmware.ToString()), "COMPASS_CAL_FIT", MainV2.comPort.MAV.param);

            mavlinkCheckBoxUseCompass1.setup(1, 0, new[] { "COMPASS_USE", "COMPASS1_USE"}, MainV2.comPort.MAV.param);
            mavlinkCheckBoxUseCompass2.setup(1, 0, new[] { "COMPASS_USE2", "COMPASS2_USE"}, MainV2.comPort.MAV.param);
            mavlinkCheckBoxUseCompass3.setup(1, 0, new[] { "COMPASS_USE3", "COMPASS3_USE"}, MainV2.comPort.MAV.param);

            CHK_compass_learn.setup(1, 0, "COMPASS_LEARN", MainV2.comPort.MAV.param);

            {
                // set the default items
                var orient_param = MainV2.comPort.MAV.param[new[] { "COMPASS_ORIENT", "COMPASS1_ORIENT"}];
                var source = ParameterMetaDataRepository.GetParameterOptionsInt(orient_param.Name,
                        MainV2.comPort.MAV.cs.firmware.ToString())
                    .Select(a => new KeyValuePair<string, string>(a.Key.ToString(), a.Value)).ToList();
                Orientation.DataSource = source;
                Orientation.DisplayMember = "Value";
                Orientation.ValueMember = "Key";
            }

            if (anymissing)
            {
                CustomMessageBox.Show("Your compass configuration has changed, please review the missing compass", Strings.ERROR);
            }
        }

        public void Deactivate()
        {
            timer1.Stop();
            magcal.Stop();

            CheckReboot();
        }

        private bool CheckReboot()
        {
            if (!MainV2.comPort.BaseStream.IsOpen)
                return true;

            if (rebootrequired)
            {
                if (CustomMessageBox.Show("Reboot required, reboot now?", "Reboot",
                        CustomMessageBox.MessageBoxButtons.YesNo) == CustomMessageBox.DialogResult.Yes)
                {
                    try
                    {
                        if (MainV2.comPort.doReboot())
                        {
                            CustomMessageBox.Show("Reboot failed. please manually reboot the hardware.", Strings.ERROR);
                        }
                        rebootrequired = false;
                    }
                    catch
                    {
                        CustomMessageBox.Show(Strings.ErrorCommunicating, Strings.ERROR);
                    }

                    return true;
                }
            }

            return false;
        }

        private async void myDataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == Up.Index && e.RowIndex != 0)
            {
                var item = list[e.RowIndex];
                list.Remove(item);
                list.Insert(e.RowIndex - 1, item);

                await UpdateFirst3();
            }

            if (e.ColumnIndex == Down.Index && e.RowIndex < (myDataGridView1.RowCount-1))
            {
                var item = list[e.RowIndex];
                list.Remove(item);
                list.Insert(e.RowIndex + 1, item);

                await UpdateFirst3();
            }
        }

        private async Task UpdateFirst3()
        {
            // write the priorities from the list itself, not the grid rows, the grid can lag
            // behind the list (Mono does not remove bound items when a row is removed)
            for (int i = 0; i < 3; i++)
            {
                var devid = 0;
                if (i < list.Count)
                {
                    list[i]._index = i;
                    devid = list[i].DevID;
                }

                bool ok = await MainV2.comPort.setParamAsync(MainV2.comPort.sysidcurrent,
                    (byte)MainV2.comPort.compidcurrent,
                    "COMPASS_PRIO" + (i + 1) + "_ID",
                    devid);

                if (!ok)
                    CustomMessageBox.Show(Strings.ErrorSettingParameter, Strings.ERROR);
            }

            rebootrequired = true;

            bs.ResetBindings(false);
            myDataGridView1.Invalidate();
        }

        private void BUT_OBmagcalstart_Click(object sender, EventArgs e)
        {
            if (rebootrequired && !CheckReboot())
            {
                return;
            }

            // the first time through, before any compass has been coloured
            if (picturedefault == Color.Empty)
                picturedefault = pictureBox1.BackColor;

            ResetMagCalDisplay();
            magcal.Start();

            try
            {
                if (!MainV2.comPort.doCommand(MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.DO_START_MAG_CAL, 0, 1, 1, 0, 0, 0, 0))
                {
                    magcal.Stop();
                    var autopilot = magcal.Messages;
                    CustomMessageBox.Show("The autopilot refused to start MAG CAL" +
                                          (autopilot.Count > 0 ? ":\n" + string.Join("\n", autopilot) : ", check it is disarmed."), Strings.ERROR);
                    return;
                }
            }
            catch (Exception ex)
            {
                magcal.Stop();
                this.LogError(ex);
                CustomMessageBox.Show("Failed to start MAG CAL, check the autopilot is still responding.\n" + ex.ToString(), Strings.ERROR);
                return;
            }

            BUT_OBmagcalaccept.Enabled = true;
            BUT_OBmagcalcancel.Enabled = true;
            timer1.Start();
        }

        private void ResetMagCalDisplay()
        {
            lbl_obmagresult.Clear();
            horizontalProgressBar1.Value = 0;
            horizontalProgressBar2.Value = 0;
            horizontalProgressBar3.Value = 0;
            pictureBox1.BackColor = picturedefault;
            pictureBox2.BackColor = picturedefault;
            pictureBox3.BackColor = picturedefault;
        }

        private void StopMagCal()
        {
            magcal.Stop();
            timer1.Stop();
            BUT_OBmagcalaccept.Enabled = false;
            BUT_OBmagcalcancel.Enabled = false;
        }

        private void BUT_OBmagcalaccept_Click(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.doCommand(MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.DO_ACCEPT_MAG_CAL, 0, 0, 1, 0, 0, 0, 0))
                {
                    CustomMessageBox.Show("The autopilot did not accept the calibration, it may not have finished yet.", Strings.ERROR);
                    return;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.ToString(), Strings.ERROR, MessageBoxButtons.OK);
                return;
            }

            StopMagCal();
            lbl_obmagresult.AppendText("Calibration accepted, please reboot the autopilot\r\n");
        }

        private void BUT_OBmagcalcancel_Click(object sender, EventArgs e)
        {
            try
            {
                if (!MainV2.comPort.doCommand(MainV2.comPort.sysidcurrent, (byte)MainV2.comPort.compidcurrent, MAVLink.MAV_CMD.DO_CANCEL_MAG_CAL, 0, 0, 1, 0, 0, 0, 0))
                {
                    CustomMessageBox.Show("The autopilot did not accept the cancel, the calibration may still be running.", Strings.ERROR);
                    return;
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Failed to cancel MAG CAL, check the autopilot is still responding.\n" + ex.ToString(), Strings.ERROR);
                return;
            }

            StopMagCal();
            ResetMagCalDisplay();
            lbl_obmagresult.AppendText("Calibration cancelled\r\n");
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            var progress = magcal.Progress;
            var reports = magcal.Reports;

            lbl_obmagresult.Clear();

            // message for user
            string message = "";
            foreach (var obj in progress.OrderBy(a => a.compass_id))
            {
                try
                {
                    if (obj.compass_id == 0)
                        horizontalProgressBar1.Value = obj.completion_pct;
                    if (obj.compass_id == 1)
                        horizontalProgressBar2.Value = obj.completion_pct;
                    if (obj.compass_id == 2)
                        horizontalProgressBar3.Value = obj.completion_pct;
                }
                catch { }

                message += "id:" + obj.compass_id + " " + obj.completion_pct.ToString() + "% ";
            }
            lbl_obmagresult.AppendText(message + "\r\n");

            foreach (var obj in reports.OrderBy(a => a.compass_id))
            {
                var failed = MagCalMonitor.IsFailed(obj);

                lbl_obmagresult.AppendText("id:" + obj.compass_id + " x:" + obj.ofs_x.ToString("0.0") + " y:" +
                                           obj.ofs_y.ToString("0.0") + " z:" +
                                           obj.ofs_z.ToString("0.0") + " fit:" + obj.fitness.ToString("0.0") + " " +
                                           (MAVLink.MAG_CAL_STATUS)obj.cal_status + "\r\n");

                if (failed)
                    lbl_obmagresult.AppendText("  " + MagCalMonitor.Describe(obj) + "\r\n");

                try
                {
                    var colour = failed ? Color.Red : Color.Green;

                    if (obj.compass_id == 0)
                    {
                        if (!failed)
                            horizontalProgressBar1.Value = 100;
                        pictureBox1.BackColor = colour;
                    }

                    if (obj.compass_id == 1)
                    {
                        if (!failed)
                            horizontalProgressBar2.Value = 100;
                        pictureBox2.BackColor = colour;
                    }

                    if (obj.compass_id == 2)
                    {
                        if (!failed)
                            horizontalProgressBar3.Value = 100;
                        pictureBox3.BackColor = colour;
                    }
                }
                catch
                {
                }

                if (failed || obj.autosaved == 1)
                    timer1.Interval = 1000;
            }

            foreach (var text in magcal.Messages)
                lbl_obmagresult.AppendText(text + "\r\n");

            if (MagCalMonitor.IsComplete(progress, reports))
            {
                StopMagCal();

                var failure = magcal.FailureMessage(reports);
                if (failure != null)
                    CustomMessageBox.Show(failure, Strings.ERROR);
                else
                    CustomMessageBox.Show("Please reboot the autopilot");
            }
        }

        private void myDataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (myDataGridView1.Rows[e.RowIndex].Cells[Priority.Index].Value?.ToString() != (e.RowIndex + 1).ToString())
            {
                myDataGridView1.Rows[e.RowIndex].Cells[Priority.Index].Value = (e.RowIndex + 1).ToString();


            }
        }

        private void but_largemagcal_Click(object sender, EventArgs e)
        {
            double value = 0;
            if (InputBox.Show("MagCal Yaw", "Enter current heading in degrees\nNOTE: gps lock is required. Heading is true, not magnetic", ref value) == DialogResult.OK)
            {
                try
                {
                    if (MainV2.comPort.doCommand(MainV2.comPort.MAV.sysid, MainV2.comPort.MAV.compid,
                        MAVLink.MAV_CMD.FIXED_MAG_CAL_YAW, (float) value, 0, 0, 0, 0, 0, 0))
                    {
                        CustomMessageBox.Show(Strings.Completed, Strings.Completed);
                    }
                    else
                    {
                        CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
                    }
                }
                catch (Exception)
                {
                    CustomMessageBox.Show(Strings.CommandFailed, Strings.ERROR);
                }
            }
        }

        private void but_reboot_Click(object sender, EventArgs e)
        {
            if (CustomMessageBox.Show("Reboot?") == CustomMessageBox.DialogResult.OK)
            {
                MainV2.comPort.doReboot(false, true);
                rebootrequired = false;
            }
        }

        private void myDataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.Cancel = true;
        }

        private void compassDeviceInfoBindingSource_CurrentChanged(object sender, EventArgs e)
        {

        }

        private async void but_missing_ClickAsync(object sender, EventArgs e)
        {
            // remove only the missing compasses from the list, then rewrite the priorities from what is left
            if (list.RemoveAll(a => a.Missing) == 0)
                return;

            await UpdateFirst3();
        }
    }
}
