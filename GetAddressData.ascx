<%@ Control Language="C#" AutoEventWireup="true" CodeFile="GetAddressData.ascx.cs" Inherits="CMSWebParts_Custom_GetAddressData" %>
<%@ Register Src="~/CMSWebParts/Custom/DateTimePicker.ascx" TagPrefix="uc1" TagName="DateTimePicker" %>

<script src="http://code.jquery.com/jquery-latest.min.js" type="text/javascript"></script>
<script src="http://cdnjs.cloudflare.com/ajax/libs/raphael/2.1.0/raphael-min.js"></script>
<script src="http://cdn.oesmith.co.uk/morris-0.4.3.min.js"></script>



<asp:Button ID="btnGetTransactions" runat="server" Text="Refresh" OnClick="btnGetTransactions_Click" /><asp:TextBox ID="txtAddress" runat="server"></asp:TextBox>
<uc1:DateTimePicker runat="server" ID="DateTimePickerStart" />
<uc1:DateTimePicker runat="server" ID="DateTimePickerEnd" />


<div style="padding:15px;">
    <asp:literal runat="server" id="table" ></asp:literal>
</div>

<div style="padding:15px;">
    <div id="chartBtcIn" style="height: 250px;"></div>
</div>
<div style="padding:15px;">
    <div id="chartBtcOut" style="height: 250px;"></div>
</div>
<div style="padding:15px;">
    <div id="chartBtcDiff" style="height: 250px;"></div>
</div>
<div style="padding:15px;">
    <div id="chartBtcBalance" style="height: 250px;"></div>
</div>
