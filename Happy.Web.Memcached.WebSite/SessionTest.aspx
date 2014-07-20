<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="SessionTest.aspx.cs" Inherits="Happy.Web.Memcached.WebSite.SessionTest" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title></title>
</head>
<body>
    <form id="form1" runat="server">
        <div>
            <asp:TextBox ID="SessionValue" runat="server" />
            <asp:Button id="SetSession" Text="设置" runat="server" OnClick="SetSession_Click" />
            <asp:Button id="GetSession" Text="读取" runat="server" OnClick="GetSession_Click" />
        </div>
    </form>
</body>
</html>
