using System;
using System.Data;
using System.Collections;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

using CMS.PortalControls;
using CMS.GlobalHelper;
using CMS.CMSHelper;
using System.Net;
using System.IO;

public partial class CMSWebParts_Custom_InputField : CMSAbstractWebPart
{
    #region "Properties"

    string blockchainURL = "http://blockchain.info/";
    string suffixQueryString = "?format=json";

    #endregion


    #region "Methods"

    /// <summary>
    /// Content loaded event handler
    /// </summary>
    public override void OnContentLoaded()
    {
        base.OnContentLoaded();
        SetupControl();
    }


    /// <summary>
    /// Initializes the control properties
    /// </summary>
    protected void SetupControl()
    {
        if (this.StopProcessing)
        {
            // Do not process
        }
        else
        {
           
            if (IsPostBack)
            {
                Page.ClientScript.RegisterStartupScript(typeof(String), "page", "GetAllData();", true);
                
            }
        }
    }


    /// <summary>
    /// Reloads the control data
    /// </summary>
    public override void ReloadData()
    {
        base.ReloadData();

        SetupControl();
    }

    #endregion


    string error = "";

    protected void btnGetData_Click(object sender, EventArgs e)
    {

        string startAddress = txtInputField.Value;

        if (!string.IsNullOrEmpty(startAddress))
        {
            string result = GetBlockchainResult(startAddress);
                      

            
        }
    }


    private string GetBlockchainResult(string startAddress)
    {

        string url = blockchainURL + "address/" +  startAddress + suffixQueryString;

        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

        HttpWebResponse response = (HttpWebResponse)request.GetResponse();

        Stream resStream = response.GetResponseStream();


        StreamReader sr = new StreamReader(resStream);
        string myStr = sr.ReadToEnd();

        return myStr;

    }
}



