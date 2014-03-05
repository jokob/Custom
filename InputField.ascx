<%@ Control Language="C#" AutoEventWireup="true" Inherits="CMSWebParts_Custom_InputField" CodeFile="~/CMSWebParts/Custom/InputField.ascx.cs" %>
<script src="http://code.jquery.com/jquery-latest.min.js" type="text/javascript"></script> 

<script src="~/CMSWebParts/Custom/InputFields/InputField.js" type="text/javascript"></script>


<div class="inputField">
    <input type="text" id="inputField" value="1CBEQ5dfUE3CwVJsLPaXP5gCv7xn9pRNZB"/><input type="button" onclick="GetBlockchainJSON('address', $('#inputField').val())" accesskey="" value="Search"/>
</div>
<div class="resultsOutput">
    <label id="output"></label>
</div>
<asp:HiddenField ClientIDMode="Static" runat="server" EnableViewState="true" ID="txtInputField"  />

<asp:Button ID="btnGetJSON" runat="server" Text="getJson" OnClick="btnGetData_Click" />




