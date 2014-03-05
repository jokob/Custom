using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;

using CMS.PortalControls;
using System.Net;
using System.IO;
using System.Data;
using Newtonsoft.Json;
using CMS.DataEngine;
using CMS.SettingsProvider;
using System.Text;




public class Input
{
    public Out prev_out { get; set; }
}

public class Out
{
    public int n { get; set; }
    public long value { get; set; }
    public string addr { get; set; }
    public long tx_index { get; set; }
    public int type { get; set; }
}

public class OutExt
{
    private DataRow dataRow;

    public OutExt()
    { }

    public OutExt(DataRow dataRow)
    {
        this.dataRow = dataRow;


        this.timeOfTrx = DateTime.Parse(dataRow["TrxTime"].ToString());
        this.hash = dataRow["TrxHash"].ToString();
        this.transactionType = dataRow["TrxType"].ToString() == "IN" ? TransactionType.IN : TransactionType.OUT;
        this.value = System.Convert.ToInt64(dataRow["TrxValue"]);
        this.balance = System.Convert.ToInt64(dataRow["TrxAfterBalance"]);
        this.tx_index = System.Convert.ToInt64(dataRow["TxIndex"]);
        this.timeEpoch = System.Convert.ToInt64(dataRow["TrxEpochTime"]);
        this.fee = System.Convert.ToInt64(dataRow["TrxFee"]);
        this.address = dataRow["TrxAddress"].ToString();
        this.fee = dataRow["TrxBlockHeight"] == System.DBNull.Value ? 0 : System.Convert.ToInt64(dataRow["TrxBlockHeight"]);
        this.itemCreatedBy = System.Convert.ToInt32(dataRow["ItemCreatedBy"]);
        this.itemCreatedWhen = DateTime.Parse(dataRow["ItemCreatedWhen"].ToString());

    }

    public DateTime timeOfTrx { get; set; }
    public TransactionType transactionType { get; set; }
    public long balance { get; set; }
    public long value { get; set; }
    public string hash { get; set; }
    public long tx_index { get; set; }
    public long timeEpoch { get; set; }
    public long fee { get; set; }
    public string address { get; set; }
    public int itemCreatedBy { get; set; }
    public DateTime itemCreatedWhen { get; set; }
    public string blockHeight { get; set; }

}

public enum TransactionType { IN, OUT };

public class Tx
{
    public long result { get; set; }
    public int block_height { get; set; }
    public int time { get; set; }
    public List<Input> inputs { get; set; }
    public long vout_sz { get; set; }
    public string relayed_by { get; set; }
    public string hash { get; set; }
    public long vin_sz { get; set; }
    public long tx_index { get; set; }
    public long ver { get; set; }
    public List<Out> @out { get; set; }
    public long size { get; set; }
}

public class BlockChainAddress
{
    public string hash160 { get; set; }
    public string address { get; set; }
    public long n_tx { get; set; }
    public long total_received { get; set; }
    public long total_sent { get; set; }
    public long final_balance { get; set; }
    public List<Tx> txs { get; set; }
}


public partial class CMSWebParts_Custom_GetAddressData : CMSAbstractWebPart
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

    protected void btnGetTransactions_Click(object sender, EventArgs e)
    {
        // check if refresh token left

        // if yes, continue
        string startAddress = txtAddress.Text;

        if (!string.IsNullOrEmpty(startAddress))
        {

            // Check if database entries for the given address are available and return last transaction if yes
            OutExt lastTrx = GetLastTransaction(startAddress);

            // no transaction found, retrieve all
            if (lastTrx == null)
            {

                List<OutExt> trxAll = new List<OutExt>();

                string url = blockchainURL + "address/" + startAddress + suffixQueryString;

                int n_offset = 0;

                // Loop until all transactions are retrieved
                while (true)
                {
                    string offset = "";

                    // append offset if necessary
                    if (n_offset > 0)
                    {
                        offset = "&offset=" + n_offset * 50;
                    }

                    string result = GetBlockchainResult(url + offset);

                    // create object from JSON
                    BlockChainAddress address = GetAddrfromJSON(result);

                    // get all transactions for the current address as dataset
                    List<OutExt> trxOrdered = GetTransactionsList(address, result);

                    trxOrdered.AddRange(trxAll);
                    trxAll = trxOrdered;

                    // Break if the total of received transaction is the same (or higher if a new transaction was issued in the mean time) as the transaction count (n_tx)
                    if (trxAll.Count >= address.n_tx)
                    {
                        break;
                    }

                    n_offset++;
                }

                trxAll = FixOrder(trxAll);

                // calculate balance 
                trxAll = CalculateBalance(trxAll);

                // generate JS for every chart
                string javascript = GetJavascript(trxAll);

                // refresh charts
                RegisterStartupScript(javascript);

                // add table
                //table.Text = GetTable(trxAll);

                // save results into DB
                SaveToDB(trxAll, startAddress);
            }
        }
    }

    private OutExt GetLastTransaction(string startAddress)
    {
        DataSet ds = ConnectionHelper.ExecuteQuery(string.Format("SELECT TOP 1 * FROM BTC_Transactions WHERE TrxAddress = '{0}' ORDER BY ItemID DESC", startAddress), null, QueryTypeEnum.SQLQuery, true);

        if (ds.Tables[0].Rows.Count > 0)
        {
            return new OutExt(ds.Tables[0].Rows[0]);
        }
        else return null;
    }


    private void SaveToDB(List<OutExt> trxAll, string address)
    {
        // Build SQL query
        StringBuilder query = new StringBuilder(@"INSERT INTO BTC_Transactions (
      [ItemCreatedBy]
      ,[ItemCreatedWhen]        
      ,[TrxHash]
      ,[TrxTime]
      ,[TrxType]
      ,[TrxValue]
      ,[TrxAfterBalance]
      ,[TxIndex]
      ,[TrxEpochTime]
      ,[TrxFee]
      ,[TrxAddress]
      ,[TrxBlockHeight]
       ) VALUES ");

        string currentUserId = CMS.CMSHelper.CMSContext.CurrentUser.UserID.ToString();
        string currentTime = DateTime.Now.ToString();

        int index = 0;

        // create SQL statement
        foreach (OutExt trx in trxAll)
        {
            query.Append("(");

            query.Append(currentUserId);
            query.Append(",");

            query.Append("'");
            query.Append(currentTime);
            query.Append("',");

            query.Append("'");
            query.Append(trx.hash);
            query.Append("',");

            query.Append("'");
            query.Append(trx.timeOfTrx);
            query.Append("',");

            query.Append("'");
            query.Append(trx.transactionType);
            query.Append("',");

            query.Append(trx.value);
            query.Append(",");

            query.Append(trx.balance);
            query.Append(",");

            query.Append(trx.tx_index);
            query.Append(",");

            query.Append(trx.timeEpoch);
            query.Append(",");

            query.Append(trx.fee);
            query.Append(",");

            query.Append("'");
            query.Append(address);
            query.Append("',");

            query.Append(trx.blockHeight == null ? "NULL" : trx.blockHeight);

            query.Append(")");

            if (trxAll.Count - 1 != index)
            {
                // if not last one, add comma
                query.Append(",");
            }


            index++;
        }

        ConnectionHelper.ExecuteQuery(query.ToString(), null, QueryTypeEnum.SQLQuery, true);
    }


    private List<OutExt> FixOrder(List<OutExt> trxAll)
    {
        for (int i = 0; i < trxAll.Count; i++)
        {
            // If epoch times are same, check, if they are in the correct order (IN transaction before the out transaction)
            if (i > 0 && trxAll[i].timeEpoch == trxAll[i - 1].timeEpoch)
            {
                // if the first transaction is an OUT transaction, swap them
                if (trxAll[i - 1].transactionType == TransactionType.OUT)
                {
                    Swap(trxAll, i, i - 1);
                }
            }
        }

        return trxAll;
    }

    static void Swap(List<OutExt> list, int indexA, int indexB)
    {
        OutExt tmp = list[indexA];
        list[indexA] = list[indexB];
        list[indexB] = tmp;
    }

    private List<OutExt> CalculateBalance(List<OutExt> trxOrdered)
    {

        trxOrdered[0].balance += trxOrdered[0].value;

        for (int i = 1; i < trxOrdered.Count; i++)
        {
            // if transaction type is IN direction, add, otherwise subtract
            trxOrdered[i].balance = trxOrdered[i - 1].balance;

            if (trxOrdered[i].transactionType == TransactionType.IN)
            {
                trxOrdered[i].balance += trxOrdered[i].value;
            }
            else
            {
                trxOrdered[i].balance -= trxOrdered[i].value;
            }
        }

        return trxOrdered;
    }



    private List<OutExt> GetTransactionsList(BlockChainAddress address, string rawData)
    {
        // List of transaction, will be ordered and use as a data source for charts

        List<OutExt> trx = new List<OutExt>();
        long balance = address.final_balance;

        // Get all relevant transactions
        foreach (Tx transaction in address.txs)
        {
            long tempValOut = 0;
            long tempValIn = 0;
            long fee = 0;

            // Use the extended out object to add a time stamp
            OutExt newItemOut = new OutExt();
            OutExt newItemIn = new OutExt();

            // Get outgoing transactions
            foreach (Input input in transaction.inputs)
            {
                if (input.prev_out.addr == address.address)
                {
                    // multiple outbound transaction are possible?
                    tempValOut += input.prev_out.value;
                }

                fee -= input.prev_out.value;
            }

            if (tempValOut != 0)
            {
                newItemOut = InitializeTrx(transaction, TransactionType.OUT, rawData, tempValOut, balance);
                trx.Add(newItemOut);
            }

            // Get incoming transactions
            foreach (Out output in transaction.@out)
            {
                if (output.addr == address.address)
                {
                    // multiple inbound transaction are possible
                    tempValIn += output.value;
                }

                fee += output.value;
            }

            if (tempValIn != 0)
            {
                newItemIn = InitializeTrx(transaction, TransactionType.IN, rawData, tempValIn, balance);
                trx.Add(newItemIn);
            }

            trx[trx.Count - 1].fee = fee;
        }

        // Order the list based on the timestamp
        return trx.OrderBy(o => o.timeOfTrx).ToList();
    }

    private OutExt InitializeTrx(Tx transaction, TransactionType transactionType, string rawData, long value, long balance)
    {
        OutExt trxEntry = new OutExt();

        trxEntry.timeEpoch = GetEpochTimeFromJSON(transaction.time, rawData, transaction.tx_index);

        // convert epoch time to regular date time
        System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        trxEntry.timeOfTrx = dtDateTime.AddSeconds(trxEntry.timeEpoch).ToLocalTime();

        trxEntry.transactionType = transactionType;
        trxEntry.value = value;
        trxEntry.hash = transaction.hash;
        trxEntry.tx_index = transaction.tx_index;
        trxEntry.balance = balance;

        return trxEntry;
    }

    private string GetJavascript(List<OutExt> transactions)
    {
        string dataOut = "";
        string dataIn = "";
        string dataDiff = "";
        string dataBallance = "";

        // Build in and out lists
        foreach (OutExt transaction in transactions)
        {
            long value = transaction.value;
            string newLine = getDataLine(transaction.timeOfTrx, value);

            if (transaction.transactionType == TransactionType.IN)
            {
                dataIn += newLine;
            }
            else
            {
                dataOut += newLine;
            }

            //Generate also differential chart
            dataDiff += getDataLine(transaction.timeOfTrx, transaction.balance);
            dataDiff += newLine;


            dataBallance += getDataLine(transaction.timeOfTrx, transaction.balance);
        }


        // finish the javascript
        string js = @"$(document).ready(
    function () {
        " +
       GetChartInitJS(dataIn, "chartBtcIn")
         +
       GetChartInitJS(dataOut, "chartBtcOut")
         +
         GetChartInitJS(dataDiff, "chartBtcDiff")
         +
         GetChartInitJS(dataBallance, "chartBtcBalance")
         +

      @"

    }
);";


        return js;
    }


    private string GetTable(List<OutExt> transactions)
    {
        string table = "<table><tr><td>Hash</td><td>Time</td><td>Type</td><td>Value</td><td>Balance</td><td>tx_index</td><td>epoch time</td><td>fee</td></tr>";

        foreach (OutExt trx in transactions)
        {
            table += string.Format("<tr><td><a href='{0}tx/{1}'>hash</a></td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td></tr>",
                blockchainURL, trx.hash, trx.timeOfTrx, trx.transactionType, ConvertToBTC(trx.value), ConvertToBTC(trx.balance), trx.tx_index, trx.timeEpoch, ConvertToBTC(trx.fee));
        }

        table += "</table>";
        return table;
    }

    private string GetChartInitJS(string dataId, string elementId)
    {
        return @"
        new Morris.Line({
            // ID of the element in which to draw the chart.
            element: '" + elementId + @"',
            // Chart data records -- each entry in this array corresponds to a point on
            // the chart.
            data: [
                    " + dataId + @"
            ],
            // The name of the data record attribute that contains x-values.
            xkey: 'date',
            // A list of names of data record attributes that contain y-values.
            ykeys: ['value'],
            // Labels for the ykeys -- will be displayed when you hover over the
            // chart.
            labels: ['BTC']
        });";
    }

    private string getDataLine(DateTime dateTime, long value)
    {
        //Example date output "2012-02-24 15:00"
        return "{ date: '" + dateTime.ToString("yyyy-MM-dd HH:mm:ss") + "', value: " + ConvertToBTC(value) + "}, \n";

    }

    private double ConvertToBTC(long value)
    {
        return (double)value / 1000000000;
    }


    private long GetEpochTimeFromJSON(int epochTime, string JSON, long tx_index)
    {
        // get the correct time from the rawData, wasn't decoded correctly from JSON de-serializer
        if (epochTime == 0)
        {
            // split the raw data to segments starting with the time value
            string[] result;
            string[] stringSeparators = new string[] { "\"time\":" };
            result = JSON.Split(stringSeparators, StringSplitOptions.None);

            // check all segments and select the one with the correct tx_index
            string found = "";

            foreach (string line in result)
            {
                if (line.Contains(tx_index.ToString()))
                {
                    found = line;

                    break;
                }
            }

            // get the time
            stringSeparators = new string[] { ",\"inputs\"" };
            result = found.Split(stringSeparators, StringSplitOptions.None);

            // convert it to integer
            epochTime = System.Convert.ToInt32(result[0]);
        }

        return epochTime;
    }

    private void RegisterStartupScript(string javascript)
    {
        Page.ClientScript.RegisterStartupScript(typeof(String), "page", javascript, true);
    }




    private BlockChainAddress GetAddrfromJSON(string result)
    {

        BlockChainAddress address = new BlockChainAddress();

        address = JsonConvert.DeserializeObject<BlockChainAddress>(result);

        return address;
    }


    private string GetBlockchainResult(string url)
    {


        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);

        HttpWebResponse response = (HttpWebResponse)request.GetResponse();

        Stream resStream = response.GetResponseStream();


        StreamReader sr = new StreamReader(resStream);
        string myStr = sr.ReadToEnd();

        return myStr;

    }


}