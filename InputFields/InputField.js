var blockchainURL = 'http://blockchain.info/';
var suffixQueryString = '?format=json';
var error = '';

var txtInputField;

window.onload = function () {
    txtInputField = $("[id*='txtInputField']");
    
};

function GetBlockchainJSON(idType, identifier) {
    var url = '';
    
    

    // initialize the url
    url = blockchainURL;

    var input = identifier;
    //alert(idType + input);


    // create correct link based on the request type
    switch (idType) {
        case 'address':
            url += 'address/';

            //Check the address format
            CheckAddressFormat(identifier);

            break;
    }

    // Check if an error wasn't detected
    if (error == '') {

        //Add ID to the URL
        url += identifier + suffixQueryString + "&cors=true";

        //
              
        txtInputField.value = url;
        $("[id*='btnGetJSON']").click();

    }
    else {
        // Error handling
        ShowError(error);
    }
}




function GetAllData()
{
    alert($("[id*='txtInputField']"));
}



function ShowError(value)
{
    alert(value);
}

function WriteOutput(result)
{
    $('#output').text(result);
}



// Simple input checking
function CheckAddressFormat(address)
{
    if (address.length != 34)
    {
        error = 'Invalid address length';
    }
    else if (address.charAt(0) != '1')
    {
        error = 'Invalid address (missing 1 at the beginning)';
    }
}



