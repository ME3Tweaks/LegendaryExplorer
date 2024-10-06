// ASI hooks this activation and sends the information to LEX.
Class SeqAct_SendMessageToLEX extends SeqAct_Log;

public static event function int GetObjClassVersion()
{
    return Super.GetObjClassVersion() + 1;
}

//class default properties can be edited in the Properties tab for the class's Default__ object.
defaultproperties
{
    VariableLinks = ({
                      LinkedVariables = (), 
                      LinkDesc = "MessageName", 
                      ExpectedType = Class'SeqVar_String', 
                      LinkVar = 'None', 
                      PropertyName = 'None', 
                      MinVars = 0, 
                      MaxVars = 1, 
                      CachedProperty = None, 
                      bWriteable = FALSE, 
                      bModifiesLinkedObject = FALSE, 
                      bAllowAnyType = FALSE
                     }, 
                     {
                      LinkedVariables = (), 
                      LinkDesc = "String", 
                      ExpectedType = Class'SeqVar_String', 
                      LinkVar = 'None', 
                      PropertyName = 'None', 
                      MinVars = 0, 
                      MaxVars = 1, 
                      CachedProperty = None, 
                      bWriteable = FALSE, 
                      bModifiesLinkedObject = FALSE, 
                      bAllowAnyType = FALSE
                     }, 
                     {
                      LinkedVariables = (), 
                      LinkDesc = "Float", 
                      ExpectedType = Class'SeqVar_Float', 
                      LinkVar = 'None', 
                      PropertyName = 'None', 
                      MinVars = 0, 
                      MaxVars = 1, 
                      CachedProperty = None, 
                      bWriteable = FALSE, 
                      bModifiesLinkedObject = FALSE, 
                      bAllowAnyType = FALSE
                     }, 
                     {
                      LinkedVariables = (), 
                      LinkDesc = "Bool", 
                      ExpectedType = Class'SeqVar_Bool', 
                      LinkVar = 'None', 
                      PropertyName = 'None', 
                      MinVars = 0, 
                      MaxVars = 1, 
                      CachedProperty = None, 
                      bWriteable = FALSE, 
                      bModifiesLinkedObject = FALSE, 
                      bAllowAnyType = FALSE
                     }, 
                     {
                      LinkedVariables = (), 
                      LinkDesc = "Int", 
                      ExpectedType = Class'SeqVar_Int', 
                      LinkVar = 'None', 
                      PropertyName = 'None', 
                      MinVars = 0, 
                      MaxVars = 1, 
                      CachedProperty = None, 
                      bWriteable = FALSE, 
                      bModifiesLinkedObject = FALSE, 
                      bAllowAnyType = FALSE
                     }, 
                     {
                      LinkedVariables = (), 
                      LinkDesc = "Vector", 
                      ExpectedType = Class'SeqVar_Vector', 
                      LinkVar = 'None', 
                      PropertyName = 'None', 
                      MinVars = 0, 
                      MaxVars = 1, 
                      CachedProperty = None, 
                      bWriteable = FALSE, 
                      bModifiesLinkedObject = FALSE, 
                      bAllowAnyType = FALSE
                     }
                    )
}