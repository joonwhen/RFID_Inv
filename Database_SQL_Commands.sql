CREATE TABLE rfid_inventory(
    epc varchar(255) NOT NULL,
    tagname varchar(255),
    tagloc varchar(255),
    tagdesc varchar(255),
    item_sn varchar(255),
    tagtime timestamp with time zone,
    item_status_automated varchar(255),
    item_status_manual varchar(255),
    personnel_checked_out varchar(255)
);

INSERT INTO rfid_inventory(epc, tagname, tagloc, tagdesc, item_sn, tagtime, item_status_automated, item_status_manual, personnel_checked_out) VALUES
('E28011606000020DBC31A25C', 'TAG1', 'tagloc', 'tagdesc', 'item s/n', '2004-10-19 10:23:54+02', 'available', 'available', 'JW')
, ('E28011606000020DBC32484C', 'TAG2', 'tagloc', 'tagdesc', 'item s/n', '2004-10-19 10:23:54+02', 'available', 'available', 'JW')
, ('E28011606000020DBC305A7E', 'TAG3', 'tagloc', 'tagdesc', 'item s/n', '2004-10-19 10:23:54+02', 'available', 'available', 'JW')
, ('E28011606000020DBC30981E', 'TAG4', 'tagloc', 'tagdesc', 'item s/n', '2004-10-19 10:23:54+02', 'available', 'available', 'JW')
, ('E2000020880101130660506C', 'CARD', 'tagloc', 'tagdesc', 'item s/n', '2004-10-19 10:23:54+02', 'available', 'available', 'JW')
;

CREATE TABLE rfid_signals(
    epc varchar(255) NOT NULL,
    antenna_no varchar(10) NOT NULL,
    RSSI varchar(10) NOT NULL,
    Last_Signal timestamp NOT NULL
);

INSERT INTO rfid_signals VALUES 
('E28011606000020DBC31A25C', '1', '1', '2016-06-22 19:10:25')
('E28011606000020DBC32484C', '1', '1', '2016-06-22 19:10:25')
('E28011606000020DBC305A7E', '1', '1', '2016-06-22 19:10:25')
('E28011606000020DBC30981E', '1', '1', '2016-06-22 19:10:25')
('E2000020880101130660506C', '1', '1', '2016-06-22 19:10:25');