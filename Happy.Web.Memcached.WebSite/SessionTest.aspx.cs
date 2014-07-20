using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Happy.Web.Memcached.WebSite
{
    public partial class SessionTest : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void SetSession_Click(object sender, EventArgs e)
        {
            this.Session["SessionValue"] = this.SessionValue.Text;
        }

        protected void GetSession_Click(object sender, EventArgs e)
        {
            this.SessionValue.Text = (string)this.Session["SessionValue"];
        }
    }
}