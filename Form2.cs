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
                string sql = "SELECT epc, tagname, tagloc, tagdesc, personnel_checked_out, time_check_out, personnel_checked_in, time_checked_in FROM rfid_inventory WHERE item_status_automated = 'Error: status do not tally.';";
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
