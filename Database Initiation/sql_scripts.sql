CREATE TABLE item_inventory (
	EPC varchar(255) NOT NULL,
	Item_Name varchar(255) NOT NULL,
	Item_Location varchar(255) NOT NULL,
	Item_Serial_Number varchar(255) NOT NULL,
	Item_Condition varchar(255) NOT NULL);

CREATE TABLE item_status (
    EPC varchar(255) NOT NULL,
    Automated_Status varchar(255),
    Manual_Status varchar(255),
    Last_Read_Time varchar(255),
    Error_Status varchar(255));

CREATE TABLE item_movement (
    EPC varchar(255) NOT NULL,
    Personnel_Checked_Out varchar(255),
    Time_Out varchar(255),
    Personnel_Checked_In varchar(255),
    Time_In varchar(255));