using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Npgsql;

namespace Inventory_Monitoring
{
    public partial class Form2 : Form
    {
        private DataSet ds = new DataSet();
        private DataTable dt = new DataTable();
        public Form2()
        {
            InitializeComponent();

            try
            {
                string connstring = String.Format("Host=localhost;Port=5432;User Id=Admin;Password=password;Database=Inventory;");
                NpgsqlConnection conn = new NpgsqlConnection(connstring);
                conn.Open();
                string sql = "SELECT item_inventory.epc, item_inventory.item_serial_number, item_inventory.item_name, item_inventory.item_condition, item_inventory.item_location, item_inventory.item_description, " +
                "item_status.manual_status, item_status.automated_status, " +
                "item_movement.personnel_checked_in, item_movement.time_in, item_movement.personnel_checked_out,item_movement.time_out " +
                "FROM item_inventory " +
                "JOIN item_status " +
                "ON item_inventory.epc = item_status.epc " +
                "JOIN item_movement " +
                "ON item_status.epc = item_movement.epc " +
                "WHERE item_status.error_status = 'Error';";
                NpgsqlDataAdapter da = new NpgsqlDataAdapter(sql, conn);
                ds.Reset();
                da.Fill(ds);
                dt = ds.Tables[0];
                dataGridView1.DataSource = dt;
                conn.Close();
            }
            catch (Exception msg)
            {
                // something went wrong, and you wanna know why
                MessageBox.Show(msg.ToString());
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }


}
