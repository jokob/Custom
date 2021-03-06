﻿using System;
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


#region JSON data objects

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

#endregion

#region Internal objects

public class Transaction
{
    private DataRow dataRow;

    public Transaction()
    { }

    public Transaction(DataRow dataRow)
    {
        this.dataRow = dataRow;


        this.Time = DateTime.Parse(dataRow["TrxTime"].ToString());
        this.Hash = dataRow["TrxHash"].ToString();
        this.TransactionType = dataRow["TrxType"].ToString() == "IN" ? TransactionType.IN : TransactionType.OUT;
        this.Value = System.Convert.ToInt64(dataRow["TrxValue"]);
        this.Balance = System.Convert.ToInt64(dataRow["TrxAfterBalance"]);
        this.Index = System.Convert.ToInt64(dataRow["TxIndex"]);
        this.TimeEpoch = System.Convert.ToInt64(dataRow["TrxEpochTime"]);
        this.Fee = System.Convert.ToInt64(dataRow["TrxFee"]);
        this.Address = dataRow["TrxAddress"].ToString();
        this.Fee = dataRow["TrxBlockHeight"] == System.DBNull.Value ? 0 : System.Convert.ToInt64(dataRow["TrxBlockHeight"]);
        this.ItemCreatedBy = System.Convert.ToInt32(dataRow["ItemCreatedBy"]);
        this.ItemCreatedWhen = DateTime.Parse(dataRow["ItemCreatedWhen"].ToString());
        this.ItemOrder = System.Convert.ToInt32(dataRow["TrxOrder"]);

    }

    public DateTime Time { get; set; }
    public TransactionType TransactionType { get; set; }
    public long Balance { get; set; }
    public long Value { get; set; }
    public string Hash { get; set; }
    // tx_index value
    public long Index { get; set; }
    public long TimeEpoch { get; set; }
    public long Fee { get; set; }
    public string Address { get; set; }
    public int ItemCreatedBy { get; set; }
    public DateTime ItemCreatedWhen { get; set; }
    public string BlockHeight { get; set; }
    // original order as reported by BlockChain
    public int ItemOrder { get; set; }

}

public enum TransactionType { IN, OUT };

#endregion


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

            lblInfo.Text = "";
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
            Transaction lastTrx = GetLastTransaction(startAddress);
                        
            // all transactions for the given address
            List<Transaction> trxAll = new List<Transaction>();

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

                // Performing remote JSON call
                string rawJSON = GetBlockchainResult(url + offset);

                // create object from JSON (deserialization)
                BlockChainAddress address = GetAddrfromJSON(rawJSON);

                bool foundLastTransaction = false;

                // get all missing transactions for the current address
                List<Transaction> trxNonOrdered = GetTransactionsList(address, rawJSON, lastTrx, trxAll.Count + (lastTrx == null ? 0 : lastTrx.ItemOrder), out foundLastTransaction);
                               
                // TODO: check if order could be reversed, move the ordering outside of the while loop and swap order values 

                 List<Transaction> trxOrdered = trxNonOrdered.OrderBy(o => o.Time).ToList();

                trxOrdered.AddRange(trxAll);
                trxAll = trxOrdered;

                // Break if the total of received transaction is the same (or higher if a new transaction was issued in the mean time) as the transaction count (n_tx)
                if (trxAll.Count >= address.n_tx || foundLastTransaction) // break if found last transaction in response
                {
                    break;
                }

                n_offset++;
            }

            if (trxAll.Count > 0)
            {

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
            else
            {
                lblInfo.Text = "Nothing retrieved.";
            }

        }
    }


    /// <summary>
    /// Retrieve last transaction from the database
    /// </summary>
    /// <param name="startAddress">Address of transaction</param>
    /// <returns></returns>
    private Transaction GetLastTransaction(string address)
    {
        DataSet ds = ConnectionHelper.ExecuteQuery(string.Format("SELECT TOP 1 * FROM BTC_Transactions WHERE TrxAddress = '{0}' ORDER BY TrxOrder DESC", address), null, QueryTypeEnum.SQLQuery, true);

        if (ds.Tables[0].Rows.Count > 0)
        {
            return new Transaction(ds.Tables[0].Rows[0]);
        }
        else return null;
    }


    /// <summary>
    /// Saving transactions to the database
    /// </summary>
    /// <param name="trxAll">List of all transactions in the correct order</param>
    /// <param name="address">Transactions address</param>
    private void SaveToDB(List<Transaction> trxAll, string address)
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
      ,[TrxOrder]
      ,[TrxFee]
      ,[TrxAddress]
      ,[TrxBlockHeight]      
       ) VALUES ");

        string currentUserId = CMS.CMSHelper.CMSContext.CurrentUser.UserID.ToString();
        string currentTime = DateTime.Now.ToString();

        int index = 0;

        // create SQL statement
        for (int i = trxAll.Count - 1; i >= 0 ; i-- )
        {
            Transaction trx = trxAll[i];

            query.Append("(");

            query.Append(currentUserId);
            query.Append(",");

            query.Append("'");
            query.Append(currentTime);
            query.Append("',");

            query.Append("'");
            query.Append(trx.Hash);
            query.Append("',");

            query.Append("'");
            query.Append(trx.Time);
            query.Append("',");

            query.Append("'");
            query.Append(trx.TransactionType);
            query.Append("',");

            query.Append(trx.Value);
            query.Append(",");

            query.Append(trx.Balance);
            query.Append(",");

            query.Append(trx.Index);
            query.Append(",");

            query.Append(trx.TimeEpoch);
            query.Append(",");
            
            query.Append(trx.ItemOrder);
            query.Append(",");

            query.Append(trx.Fee);
            query.Append(",");

            query.Append("'");
            query.Append(address);
            query.Append("',");

            query.Append(trx.BlockHeight == null ? "NULL" : trx.BlockHeight);

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


    /// <summary>
    /// Fixing transactions order
    /// </summary>
    /// <param name="trxAll">List of all transactions</param>    
    private List<Transaction> FixOrder(List<Transaction> trxAll)
    {
        for (int i = 0; i < trxAll.Count; i++)
        {
            // If epoch times are same, check, if they are in the correct order (IN transaction before the out transaction)
            if (i > 0 && trxAll[i].TimeEpoch == trxAll[i - 1].TimeEpoch)
            {
                // if the first transaction is an OUT transaction, swap them
                if (trxAll[i - 1].TransactionType == TransactionType.OUT)
                {
                    Swap(trxAll, i, i - 1);
                }
            }
        }

        return trxAll;
    }


    /// <summary>
    /// Function for swapping two transactions in the LIst for reordering
    /// </summary>
    /// <param name="list">Source list</param>
    /// <param name="indexA">Index of first item</param>
    /// <param name="indexB">Index of second item</param>
    static void Swap(List<Transaction> list, int indexA, int indexB)
    {
        Transaction tmp = list[indexA];
        list[indexA] = list[indexB];
        list[indexB] = tmp;
    }


    /// <summary>
    /// Balance calculation
    /// </summary>
    /// <param name="trxOrdered">List of ordered transactions</param>    
    private List<Transaction> CalculateBalance(List<Transaction> trxOrdered)
    {

        trxOrdered[0].Balance += trxOrdered[0].Value;

        for (int i = 1; i < trxOrdered.Count; i++)
        {
            // if transaction type is IN direction, add, otherwise subtract
            trxOrdered[i].Balance = trxOrdered[i - 1].Balance;

            if (trxOrdered[i].TransactionType == TransactionType.IN)
            {
                trxOrdered[i].Balance += trxOrdered[i].Value;
            }
            else
            {
                trxOrdered[i].Balance -= trxOrdered[i].Value;
            }
        }

        return trxOrdered;
    }


    /// <summary>
    /// Extracts all transactions from the BlockChain JSON response
    /// </summary>
    /// <param name="address">The address of the report</param>
    /// <param name="rawData">Serialized JSON data for invalidly serialized data</param>    
    private List<Transaction> GetTransactionsList(BlockChainAddress address, string rawData, Transaction lastTrx, int order, out bool lastTrxFound)
    {
        // List of transaction, will be ordered and use as a data source for charts
        List<Transaction> trx = new List<Transaction>();

        // value for balance calculation
        long balance = address.final_balance;
        lastTrxFound = false;

        // Get all relevant transactions
        foreach (Tx transaction in address.txs)
        {
            if (lastTrx != null && transaction.tx_index == lastTrx.Index)
            {
                lastTrxFound = true;
                break;
            }

            long tempValOut = 0;
            long tempValIn = 0;
            long fee = 0;

            // Use the extended out object to add a time stamp
            Transaction newItemOut = new Transaction();
            Transaction newItemIn = new Transaction();

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
                newItemOut = InitializeTrx(transaction, TransactionType.OUT, rawData, tempValOut, balance, order);
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
                newItemIn = InitializeTrx(transaction, TransactionType.IN, rawData, tempValIn, balance, order);                
                trx.Add(newItemIn);
            }

            trx[trx.Count - 1].Fee = fee < 0 ?  fee * -1 : fee;
        }


        // initialize the original transaction order (will be reversed later)        
        for (int i =trx.Count - 1; i >=0 ; i--)
        {
            trx[i].ItemOrder += trx[i].ItemOrder + i;
        }

        // Order the list based on the timestamp
        return trx;
    }


    /// <summary>
    /// Initializes the transaction with values
    /// </summary>
    /// <param name="transaction">JSON transaction deserialized</param>
    /// <param name="transactionType">IN or OUT transaction</param>
    /// <param name="rawData">The JSON raw data (serialized) for extracting missing values</param>
    /// <param name="value">Transaction value/amount</param>
    /// <param name="balance">The balance of the address after the transaction</param>
    /// <param name="itemOrder">The order/index of the transaction according to BlockChain</param>
    /// <returns>Initialized extended transaction</returns>
    private Transaction InitializeTrx(Tx transaction, TransactionType transactionType, string rawData, long value, long balance, int itemOrder)
    {
        Transaction trxEntry = new Transaction();

        trxEntry.TimeEpoch = GetEpochTimeFromJSON(transaction.time, rawData, transaction.tx_index);

        // convert epoch time to regular date time
        System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        trxEntry.Time = dtDateTime.AddSeconds(trxEntry.TimeEpoch).ToLocalTime();

        trxEntry.TransactionType = transactionType;
        trxEntry.Value = value;
        trxEntry.Hash = transaction.hash;
        trxEntry.Index = transaction.tx_index;
        trxEntry.Balance = balance;
        trxEntry.ItemOrder = itemOrder;

        return trxEntry;
    }


    /// <summary>
    /// Generates the JavaScript for the charts
    /// </summary>
    /// <param name="transactions">List of transactions to be plotted</param>
    /// <returns>JavaScript</returns>
    private string GetJavascript(List<Transaction> transactions)
    {
        string dataOut = "";
        string dataIn = "";
        string dataDiff = "";
        string dataBallance = "";

        // Build in and out lists
        foreach (Transaction transaction in transactions)
        {
            long value = transaction.Value;
            string newLine = getDataLine(transaction.Time, value);

            if (transaction.TransactionType == TransactionType.IN)
            {
                dataIn += newLine;
            }
            else
            {
                dataOut += newLine;
            }

            //Generate also differential chart
            dataDiff += getDataLine(transaction.Time, transaction.Balance);
            dataDiff += newLine;


            dataBallance += getDataLine(transaction.Time, transaction.Balance);
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


    /// <summary>
    /// Generates an HTML table for transactions
    /// </summary>
    /// <param name="transactions">List of transactions</param>
    /// <returns>HTML</returns>
    private string GetTable(List<Transaction> transactions)
    {
        string table = "<table><tr><td>Hash</td><td>Time</td><td>Type</td><td>Value</td><td>Balance</td><td>tx_index</td><td>epoch time</td><td>fee</td></tr>";

        foreach (Transaction trx in transactions)
        {
            table += string.Format("<tr><td><a href='{0}tx/{1}'>hash</a></td><td>{2}</td><td>{3}</td><td>{4}</td><td>{5}</td><td>{6}</td><td>{7}</td><td>{8}</td></tr>",
                blockchainURL, trx.Hash, trx.Time, trx.TransactionType, ConvertToBTC(trx.Value), ConvertToBTC(trx.Balance), trx.Index, trx.TimeEpoch, ConvertToBTC(trx.Fee));
        }

        table += "</table>";
        return table;
    }


    /// <summary>
    /// Generates the JavaScript for a line chart 
    /// </summary>
    /// <param name="data">String formatted according the morris documentation</param>
    /// <param name="elementId">The ID of the DIV where the chart should be displayed</param>
    /// <returns></returns>
    private string GetChartInitJS(string data, string elementId)
    {
        return @"
        new Morris.Line({
            // ID of the element in which to draw the chart.
            element: '" + elementId + @"',
            // Chart data records -- each entry in this array corresponds to a point on
            // the chart.
            data: [
                    " + data + @"
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


    /// <summary>
    /// Converts the value of the transaction to BTC
    /// </summary>
    /// <param name="value">Value</param>
    /// <returns>Value in BTC</returns>
    private double ConvertToBTC(long value)
    {
        return (double)value / 1000000000;
    }


    /// <summary>
    /// Method for converting epoch time to date and time and extracting the time from the serialized JSON data if necessary.
    /// </summary>
    /// <param name="epochTime">The epoch time value</param>
    /// <param name="JSON">The serialized JSON data</param>
    /// <param name="tx_index">The index of the transaction</param>
    /// <returns>Date and time of the transaction</returns>
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


    /// <summary>
    /// Registers the JavaScript
    /// </summary>
    /// <param name="javascript">JavaScript to register</param>
    private void RegisterStartupScript(string javascript)
    {
        Page.ClientScript.RegisterStartupScript(typeof(String), "page", javascript, true);
    }


    /// <summary>
    /// Deserializing the JSON object
    /// </summary>
    /// <param name="result"></param>
    /// <returns></returns>
    private BlockChainAddress GetAddrfromJSON(string result)
    {

        BlockChainAddress address = new BlockChainAddress();

        address = JsonConvert.DeserializeObject<BlockChainAddress>(result);

        return address;
    }


    /// <summary>
    /// Retrieval of JSON response from BlockChain
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
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