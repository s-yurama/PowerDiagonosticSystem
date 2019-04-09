// --------
// Settings
// --------
// custome data module id
const string CUSTOM_DATA_ID_LCD_STATUS_FOR_DETAIL     = "pds_detail";
const string CUSTOM_DATA_ID_LCD_STATUS_FOR_BATTERY    = "pds_batteries";
const string CUSTOM_DATA_ID_LCD_STATUS_FOR_SOLARPANEL = "pds_solarpanels";
const string CUSTOM_DATA_ID_LCD_STATUS_FOR_HYDROTANK  = "pds_hydrotank";

// --------
// Messages
// --------
// Error
const string ERROR_UPDATE_TYPE_INVALID = "Invalid update types.";
const string ERROR_BLOCKS_NOT_FOUND    = "Loading blocks is failure.";
//const string ERROR_COCKPIT_NOT_FOUND   = "Identified Cockpit Not Found.";

// --------
// Class
// --------
Blocks       blocks;
ErrorHandler error;
PDS          pds;

// --------
// run interval
// --------
const double EXEC_FRAME_RESOLUTION = 15;
const double EXEC_INTERVAL_TICK = 1 / EXEC_FRAME_RESOLUTION;
double currentTime = 0;

// --------
// update interval
// --------
const int UPDATE_INTERVAL = 10;
double updateTimer = 0;

public Program()
{
    // The constructor, called only once every session and
    // always before any other method is called. Use it to
    // initialize your script. 
    //     
    // The constructor is optional and can be removed if not
    // needed.
    // 
    // It's recommended to set RuntimeInfo.UpdateFrequency 
    // here, which will allow your script to run itself without a 
    // timer block.

    updateTimer = UPDATE_INTERVAL;
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

public void Save()
{
    // Called when the program needs to save its state. Use
    // this method to save your state to the Storage field
    // or some other means. 
    // 
    // This method is optional and can be removed if not
    // needed.
}

public void Main(string argument, UpdateType updateSource)
{
    // The main entry point of the script, invoked every time
    // one of the programmable block's Run actions are invoked,
    // or the script updates itself. The updateSource argument
    // describes where the update came from.
    // 
    // The method itself is required, but the arguments above
    // can be removed if not needed.

    if ( error == null ) {
        error = new ErrorHandler(this);
    }

    checkUpdateType(updateSource);

    currentTime += Runtime.TimeSinceLastRun.TotalSeconds;
    if (currentTime < EXEC_INTERVAL_TICK) {
        return;
    }

    procedure();

    error.echo();

    currentTime = 0;
}

private void checkUpdateType(UpdateType updateSource)
{
    // check updateTypes
    if( (updateSource & ( UpdateType.Update1 | UpdateType.Update10 | UpdateType.Update100 | UpdateType.Once )) == 0 ) {
        error.add(ERROR_UPDATE_TYPE_INVALID);
    }
}

/**
 * main control procedure
 */
private void procedure()
{
    updateTimer += currentTime;

    if (updateTimer < UPDATE_INTERVAL) {
        Echo($"next refresh: {UPDATE_INTERVAL - updateTimer:0}");
    } else {    
        updateTimer = 0;
        Echo("updating...");

        this.blocks = new Blocks(GridTerminalSystem, Me.CubeGrid, error);
    }

    if( error.isExists() ) {
        return;
    }

    if ( pds == null ) {
        pds = new PDS( this );
    }

    pds.updateSolarPanels(blocks.solarPanelsList);
    pds.updateBatteries(blocks.batteriesList);
    pds.updateHydrogenTanks(blocks.hydrogenTanksList);

    pds.main(PDS.TYPE.DETAIL,        blocks.textPanelsForDetail);
    pds.main(PDS.TYPE.SOLARPANEL,    blocks.textPanelsForSolarPanel);
    pds.main(PDS.TYPE.BATTERY,       blocks.textPanelsForBattery, true);
    pds.main(PDS.TYPE.HYDROGEN_TANK, blocks.textPanelsForHydrogenTanks, true);
}

private class Blocks
{
    IMyGridTerminalSystem grid;
    IMyCubeGrid           cubeGrid;
    ErrorHandler          error;

    List<IMyTerminalBlock> ownGridBlocksList;

    public List<IMyTextPanel> textPanelsForDetail;
    public List<IMyTextPanel> textPanelsForBattery;
    public List<IMyTextPanel> textPanelsForSolarPanel;
    public List<IMyTextPanel> textPanelsForHydrogenTanks;

    public List<IMyBatteryBlock> batteriesList;
    public List<IMySolarPanel>   solarPanelsList;
    public List<IMyGasTank>      hydrogenTanksList;

    // default constructor
    public Blocks(
        IMyGridTerminalSystem grid,
        IMyCubeGrid cubeGrid,
        ErrorHandler error
    )
    {
        this.grid     = grid;
        this.cubeGrid = cubeGrid;
        this.error    = error;

        this.textPanelsForDetail        = new List<IMyTextPanel>();
        this.textPanelsForBattery       = new List<IMyTextPanel>();
        this.textPanelsForSolarPanel    = new List<IMyTextPanel>();
        this.textPanelsForHydrogenTanks = new List<IMyTextPanel>();

        this.batteriesList     = new List<IMyBatteryBlock>();
        this.solarPanelsList   = new List<IMySolarPanel>();
        this.hydrogenTanksList = new List<IMyGasTank>();

        this.error.clear();

        this.updateOwenedBlocks();

        if( ! this.isOwened() ) { 
            error.add(ERROR_BLOCKS_NOT_FOUND);
            return;
        }

        this.clear();

        this.assign();
    }

    private void updateOwenedBlocks()
    {
        this.ownGridBlocksList = new List<IMyTerminalBlock>();

        grid.GetBlocksOfType<IMyMechanicalConnectionBlock>(this.ownGridBlocksList);

        HashSet<IMyCubeGrid> CubeGridSet = new HashSet<IMyCubeGrid>();
        CubeGridSet.Add(this.cubeGrid);

        bool isExists;
        IMyMechanicalConnectionBlock block;

        // get all of CubeGrid by end of it
        // this is neccessary if you put cockpit on subgrid (ex: turret)
        do{
            isExists = false;
            for( int i = 0; i < this.ownGridBlocksList.Count; i++ ) {
                block = this.ownGridBlocksList[i] as IMyMechanicalConnectionBlock;

                if ( CubeGridSet.Contains(block.CubeGrid) || CubeGridSet.Contains(block.TopGrid) ) {
                    CubeGridSet.Add(block.CubeGrid);
                    CubeGridSet.Add(block.TopGrid);
                    this.ownGridBlocksList.Remove(this.ownGridBlocksList[i]);
                    isExists = true;
                }
            }
        }
        while(isExists);

        //get filtered block
        this.ownGridBlocksList.Clear();

        grid.GetBlocksOfType<IMyTerminalBlock>(this.ownGridBlocksList, owenedBlock => CubeGridSet.Contains(owenedBlock.CubeGrid));
    }

    private bool isOwened()
    {
        if (this.ownGridBlocksList.Count > 0) {
            return true;
        }
        return false;
    }

    private void clear()
    {
        this.textPanelsForDetail.Clear();
        this.textPanelsForBattery.Clear();
        this.textPanelsForSolarPanel.Clear();

        this.batteriesList.Clear();
        this.solarPanelsList.Clear();
        this.hydrogenTanksList.Clear();
    }

    private void assign()
    {
        List<IMyTextPanel> textPanelsList = new List<IMyTextPanel>();

        foreach ( IMyTerminalBlock block in this.ownGridBlocksList ) {
            if ( block is IMyTextPanel ) {
                textPanelsList.Add(block as IMyTextPanel);
                continue;
            }
            if ( block is IMySolarPanel ) {
                this.solarPanelsList.Add(block as IMySolarPanel);
                continue;
            }
            if ( block is IMyBatteryBlock ) {
                this.batteriesList.Add(block as IMyBatteryBlock);
                continue;
            }
            if ( block is IMyGasTank ) {
                this.hydrogenTanksList.Add(block as IMyGasTank);
                continue;
            }
        }

        foreach ( IMyTextPanel textPanel in textPanelsList ) {
            if ( textPanel.CustomData.Contains(CUSTOM_DATA_ID_LCD_STATUS_FOR_DETAIL) ) {
                this.textPanelsForDetail.Add(textPanel);
                continue;
            }
            if ( textPanel.CustomData.Contains(CUSTOM_DATA_ID_LCD_STATUS_FOR_BATTERY) ) {
                this.textPanelsForBattery.Add(textPanel);
                continue;
            }
            if ( textPanel.CustomData.Contains(CUSTOM_DATA_ID_LCD_STATUS_FOR_SOLARPANEL) ){
                this.textPanelsForSolarPanel.Add(textPanel);
                continue;
            }
            if ( textPanel.CustomData.Contains(CUSTOM_DATA_ID_LCD_STATUS_FOR_HYDROTANK) ){
                this.textPanelsForHydrogenTanks.Add(textPanel);
                continue;
            }
        }
    }
}

private class ErrorHandler
{
    private Program program;
    private List<string> errorList = new List<string>();

    public ErrorHandler(Program program)
    {
        this.program = program;
    }

    public bool isExists()
    {
        if ( this.errorList.Count > 0 ) {
            return true;
        }
        return false;
    }

    public void add(string error)
    {
        this.errorList.Add(error);
    }

    public void clear()
    {
        this.errorList.Clear();
    }

    public void echo()
    { 
        foreach ( string error in this.errorList ) {
            this.program.Echo("Error: " + error);
        }
    }
}

class PDS
{
    public enum TYPE {
        DETAIL        = 0,
        BATTERY       = 1,
        SOLARPANEL    = 2,
        HYDROGEN_TANK = 3,
    }

    public struct DetailSolarPanel 
    { 
       public int   qty; 
       public double totalOutput;    
       public double totalMaxOutput;    
       public double fEfficiency;
    }    

    public struct DetailBattery 
    { 
       public int   qty; 
       public double totalInput;    
       public double totalOutput;    
       public double totalStored;    
       public double totalMaxStored; 
    }
    
    public struct DetailHydrogenTank
    { 
       public int   qty; 
       //public float totalInput;    
       //public float totalOutput;    
       public double totalStored;    
       public double totalMaxStored; 
    }
    
    private Program program;

    bool invert = false;
        
    DetailSolarPanel   detailSolarPanel;   
    DetailBattery      detailBattery;
    DetailHydrogenTank detailHydrogenTanks;

    int width  = 132; //178;
    int height =  89; //263;

    double chartSizeOut = 0.20d;
    double chartSizeIn  = 0.07d;
    double chartSize_bar_out;
    double chartSize_bar_in;

    int center_text_width;     
    int center_text_height; 

    StringBuilder sb;

    char[,] percentage = new char[13,41];   

    public PDS(Program program)
    {
        this.program = program;
        
        this.chartSize_bar_out = this.chartSizeOut - 0.0107d;
        this.chartSize_bar_in  = this.chartSizeIn  + 0.01d;

        this.sb = new StringBuilder("", this.width * this.height);
    }

    public void updateSolarPanels(List<IMySolarPanel> solarPanelsList)
    {
        this.detailSolarPanel.qty            = solarPanelsList.Count; 
        this.detailSolarPanel.totalOutput    = 0.0d;  
        this.detailSolarPanel.totalMaxOutput = 0.0d;  

        for (int i = 0; i < this.detailSolarPanel.qty; i++)         
        {               
            //string name = solarPanelsList[i].CustomName;
            this.detailSolarPanel.totalOutput    += solarPanelsList[i].MaxOutput;            
            this.detailSolarPanel.totalMaxOutput += 0.16d; //solarPanelsList[i].MaxOutput;             
        }
        this.detailSolarPanel.fEfficiency = this.detailSolarPanel.totalOutput/detailSolarPanel.totalMaxOutput*100;
    }

    public void updateBatteries(List<IMyBatteryBlock> batteriesList)
    {
        this.detailBattery.qty            = batteriesList.Count; 
        this.detailBattery.totalInput     = 0.0d;  
        this.detailBattery.totalOutput    = 0.0d;  
        this.detailBattery.totalStored    = 0.0d;  
        this.detailBattery.totalMaxStored = 0.0d;  

        for (int i = 0; i < this.detailBattery.qty; i++) {
            //string name = batteriesList[i].CustomName;
            this.detailBattery.totalInput     += batteriesList[i].CurrentInput;             
            this.detailBattery.totalOutput    += batteriesList[i].CurrentOutput;              
            this.detailBattery.totalStored    += batteriesList[i].CurrentStoredPower;            
            this.detailBattery.totalMaxStored += batteriesList[i].MaxStoredPower;            
         }  
    }
    
    public void updateHydrogenTanks(List<IMyGasTank> hydrogenTanksList)
    {
        this.detailHydrogenTanks.qty            = hydrogenTanksList.Count; 
        this.detailHydrogenTanks.totalStored    = 0.0d;  
        this.detailHydrogenTanks.totalMaxStored = 0.0d;  

        for (int i = 0; i < this.detailHydrogenTanks.qty; i++) {               
            //string name = hydrogenTanksList[i].CustomName;
            //this.detailHydrogenTank.totalInput     += hydrogenTanksList[i].CurrentInput;             
            //this.detailHydrogenTank.totalOutput    += hydrogenTanksList[i].CurrentOutput;              
            this.detailHydrogenTanks.totalStored    += hydrogenTanksList[i].Capacity * hydrogenTanksList[i].FilledRatio;            
            this.detailHydrogenTanks.totalMaxStored += hydrogenTanksList[i].Capacity;            
         }  
    }

    public void main(TYPE type, List<IMyTextPanel> tp, bool invert = false)
    {
        this.sb.Clear();
        this.invert = invert;

        if (type == TYPE.DETAIL && tp.Count > 0) {
            this.createText_Detail();
            tp[0].WriteText(this.sb.ToString(), false);
        }

        if (type == TYPE.BATTERY && tp.Count > 0) {
            this.createChart_Battery();
            tp[0].WriteText(this.sb.ToString(), false);
        }

        if (type == TYPE.SOLARPANEL && tp.Count > 0) {
            this.createChart_SolarPanel();
            tp[0].WriteText(this.sb.ToString(), false);
        }

        if (type == TYPE.HYDROGEN_TANK && tp.Count > 0) {
            this.createChart_HydrogenTank();
            tp[0].WriteText(this.sb.ToString(), false);
        }
    }

    private void createText_Detail()    
    {    
        this.sb.Append("\r\n");    

        this.sb.Append(" Power Diagnostic System\r\n");
        this.sb.Append("\r\n");    

        this.sb.Append(" ? Solar Panel Status\r\n");

        if ( this.detailSolarPanel.qty > 0 ) 
        {         
            this.sb.Append("     output     : ");
            this.sb.Append((this.detailSolarPanel.totalOutput).ToString("0.00"));
            this.sb.Append("MW\r\n");
            this.sb.Append("     Max output : ");
            this.sb.Append((this.detailSolarPanel.totalMaxOutput).ToString("0.00"));
            this.sb.Append("MW\r\n");
            this.sb.Append("     Efficiency : ");
            this.sb.Append((this.detailSolarPanel.fEfficiency).ToString("0.00"));
            this.sb.Append("%\r\n");
        } 
        else 
        { 
           this.sb.Append("     solar panel not found.\r\n");
        } 

        this.sb.Append("\r\n");    

        this.sb.Append(" ? Battery Status\r\n");

        if ( this.detailBattery.qty > 0 ) 
        {               
            this.sb.Append("     Input            : ");
            this.sb.Append((this.detailBattery.totalInput).ToString("0.00"));
            this.sb.Append("MW\r\n");
            this.sb.Append("     Output           : ");
            this.sb.Append((this.detailBattery.totalOutput).ToString("0.00"));
            this.sb.Append("MW\r\n");
            this.sb.Append("     Stored Power     : ");
            this.sb.Append((this.detailBattery.totalStored).ToString("0.00"));
            this.sb.Append("MWh\r\n");
            this.sb.Append("     Max Stored Power : ");
            this.sb.Append((this.detailBattery.totalMaxStored).ToString("0.00"));
            this.sb.Append("MWh\r\n");
            this.sb.Append("     Stored           : ");
            this.sb.Append((this.detailBattery.totalStored/detailBattery.totalMaxStored*100).ToString("0.00"));
            this.sb.Append("%\r\n");   
        } 
        else 
        { 
           this.sb.Append("     battery not found.\r\n");  
        }
        
        this.sb.Append("\r\n");    

        this.sb.Append(" ? Hydrogen Fuel Status\r\n");

        if ( this.detailHydrogenTanks.qty > 0 ) 
        {               
            this.sb.Append("     Stored Fuel     : ");
            this.sb.Append((this.detailHydrogenTanks.totalStored).ToString("0.00"));
            this.sb.Append("L\r\n");
            this.sb.Append("     Max Stored Fuel : ");
            this.sb.Append((this.detailHydrogenTanks.totalMaxStored).ToString("0.00"));
            this.sb.Append("L\r\n");
            this.sb.Append("     Stored          : ");
            this.sb.Append((this.detailHydrogenTanks.totalStored/detailHydrogenTanks.totalMaxStored*100).ToString("0.00"));
            this.sb.Append("%\r\n");   
        } 
        else 
        { 
           this.sb.Append("     hydrogen tank not found.\r\n");  
        }
    }

    private void createChart_SolarPanel()
    { 
        double rate;

        this.sb.Append("\r\n");        

        this.sb.Append("  ???  ???  ?      ?   ????  ????    ?   ?   ? ????? ?   \r\n");
        this.sb.Append(" ?    ?   ? ?     ? ?  ?   ? ?   ?  ? ?  ??  ? ?     ?   \r\n");
        this.sb.Append("  ??  ?   ? ?    ????? ????? ????  ????? ? ? ? ????? ?   \r\n");
        this.sb.Append("    ? ?   ? ?    ?   ? ?  ?  ?     ?   ? ?  ?? ?     ?   \r\n");
        this.sb.Append(" ???   ???  ???? ?   ? ?   ? ?     ?   ? ?   ? ????? ????\r\n");

        if (this.detailSolarPanel.totalMaxOutput > 0.0d)
        { 
            rate = this.detailSolarPanel.totalOutput/this.detailSolarPanel.totalMaxOutput;
        } 
        else
        { 
            rate = 0.0d;
        }

        createChart(rate);
    }    
    
    private void createChart_Battery()
    {        
        double rate;

        this.sb.Append("\r\n");      

        this.sb.Append(" ???    ?   ????? ????? ????? ????  ?   ?\r\n");
        this.sb.Append(" ?  ?  ? ?    ?     ?   ?     ?   ?  ? ?\r\n");
        this.sb.Append(" ???  ?????   ?     ?   ????? ?????   ? \r\n");
        this.sb.Append(" ?  ? ?   ?   ?     ?   ?     ?  ?    ?\r\n");
        this.sb.Append(" ???  ?   ?   ?     ?   ????? ?   ?   ?r\n");

        if (this.detailBattery.totalMaxStored > 0.0d)
        { 
           rate = this.detailBattery.totalStored/this.detailBattery.totalMaxStored;
        } 
        else 
        { 
           rate = 0.0d;
        }

        createChart(rate);
    }

    private void createChart_HydrogenTank()
    {        
        double rate;

        this.sb.Append("\r\n");      

        this.sb.Append(" ?  ? ?   ? ????  ????   ???   ???  ????? ?    ?\r\n");
        this.sb.Append(" ?  ?  ? ?  ?   ? ?   ? ?   ? ?     ?     ??   ?\r\n");
        this.sb.Append(" ????   ?   ?   ? ????  ?   ? ? ??? ????? ? ?  ?\r\n");
        this.sb.Append(" ?  ?   ?   ?   ? ?  ?  ?   ? ?   ? ?     ?  ? ?\r\n");
        this.sb.Append(" ?  ?   ?   ????  ?   ?  ???   ???  ????? ?   ??\r\n");

        if (this.detailHydrogenTanks.totalMaxStored > 0.0d)
        { 
           rate = this.detailHydrogenTanks.totalStored / this.detailHydrogenTanks.totalMaxStored;
        } 
        else 
        { 
           rate = 0.0d;
        }

        createChart(rate);
    }

    private void createChart(double rate)
    {
        string dot = "?";
        //string bg  = " ";
        
        int center_v = height / 2;       
        int center_h = width  / 2;   

        int x;   
        int y;
        double square;  
        double square_y;  
        double square_c;

        int x_txt_center = center_text_width  / 2;   
        int y_txt_center = center_text_height / 2;
        
        double rate_rad;

        square_c = Math.Pow(center_v,2) + Math.Pow(center_h,2);  

        createPercentage(rate);
        
        if (this.invert) {
            dot = " ";
            //bg  = "?";
        }

        rate_rad = 2.0d * Math.PI * rate;
        
        for ( int v = 0; v < height; v++ )       
        {  
            y = v - center_v;  
            square_y = y * y;  

            for(int h = 0; h < width; h++)  
            {   
                x = (h - center_h) * height / width;   

                square = square_y + x * x;   

                if ( true  
                     && square < square_c * chartSizeOut   
                     && square > square_c * chartSize_bar_out 
                )     
                {  
                    this.sb.Append("?");
                    continue;     
                }     

                if ( true       
                    && square < square_c * chartSize_bar_out  
                    && square > square_c * chartSize_bar_in
                    && Math.Atan2(-x, y ) + Math.PI > rate_rad
                )
                {
                    this.sb.Append(dot);
                    continue;      
                }     

                if ( true
                     && square < square_c * chartSize_bar_in   
                     && square > square_c * chartSizeIn   
                )       
                {        
                    this.sb.Append("?");
                    continue;       
                }  

                if ( true  
                     && -y_txt_center <= y && y <= y_txt_center   
                     && -x_txt_center <= x && x <= x_txt_center   
                )       
                {        
                    this.sb.Append(percentage[y+y_txt_center , x+x_txt_center ]);       
                    continue;       
                }   
                this.sb.Append(" ");
            }    
            this.sb.Append("\r\n");
        }
    }
  
    private void createPercentage(double rate)
    { 
        int rate2; 
        int rate3; 

        string[] degit; 

        //Echo ("rate:" + rate); 

        // if it's 100%
        if ( rate == 1 ) 
        { 
            rate2 = 0; 
            rate3 = 0; 

            degit = new string[] {  
                "     ?? " + fontNumber[rate2,  0] + fontNumber[rate3,  0] + "           ",
                "     ?? " + fontNumber[rate2,  1] + fontNumber[rate3,  1] + "           ",
                "     ?? " + fontNumber[rate2,  2] + fontNumber[rate3,  2] + "           ",
                "     ?? " + fontNumber[rate2,  3] + fontNumber[rate3,  3] + "           ",
                "     ?? " + fontNumber[rate2,  4] + fontNumber[rate3,  4] + "           ",
                "     ?? " + fontNumber[rate2,  5] + fontNumber[rate3,  5] + "           ",
                "        " + fontNumber[rate2,  6] + fontNumber[rate3,  6] + " ???   ?   ",
                "     ?? " + fontNumber[rate2,  7] + fontNumber[rate3,  7] + " ? ?  ?    ",
                "     ?? " + fontNumber[rate2,  8] + fontNumber[rate3,  8] + " ? ? ??    ",
                "     ?? " + fontNumber[rate2,  9] + fontNumber[rate3,  9] + " ??? ? ??? ",
                "     ?? " + fontNumber[rate2, 10] + fontNumber[rate3, 10] + "    ?? ? ? ",
                "     ?? " + fontNumber[rate2, 11] + fontNumber[rate3, 11] + "    ?  ? ? ",
                "     ?? " + fontNumber[rate2, 12] + fontNumber[rate3, 12] + "   ?   ??? "
            }; 
        } 
        else 
        { 
            rate2 = (int)(rate %   1 /  0.1); 
            rate3 = (int)(rate % 0.1 / 0.01); 

            degit = new string[] {  
                "        " + fontNumber[rate2,  0] + fontNumber[rate3,  0] + "           ",
                "        " + fontNumber[rate2,  1] + fontNumber[rate3,  1] + "           ",
                "        " + fontNumber[rate2,  2] + fontNumber[rate3,  2] + "           ",
                "        " + fontNumber[rate2,  3] + fontNumber[rate3,  3] + "           ",
                "        " + fontNumber[rate2,  4] + fontNumber[rate3,  4] + "           ",
                "        " + fontNumber[rate2,  5] + fontNumber[rate3,  5] + "           ",
                "        " + fontNumber[rate2,  6] + fontNumber[rate3,  6] + " ???   ?   ",
                "        " + fontNumber[rate2,  7] + fontNumber[rate3,  7] + " ? ?  ?    ",
                "        " + fontNumber[rate2,  8] + fontNumber[rate3,  8] + " ? ? ??    ",
                "        " + fontNumber[rate2,  9] + fontNumber[rate3,  9] + " ??? ? ??? ",
                "        " + fontNumber[rate2, 10] + fontNumber[rate3, 10] + "    ?? ? ? ",
                "        " + fontNumber[rate2, 11] + fontNumber[rate3, 11] + "    ?  ? ? ",
                "        " + fontNumber[rate2, 12] + fontNumber[rate3, 12] + "   ?   ??? "
            }; 
        } 

        /* 29 x 9 */ 
        center_text_height = degit.Count(); 

        for(int i = 0; i < degit.Count(); i++)  
        {  
            char[] percentage_temp = degit[i].ToCharArray();  

            for(int j=0; j < percentage_temp.Count(); j++) 
            { 
                //this.program.Echo ("i:" + i + "\r\n"); 
                //this.program.Echo ("j:" + j + "\r\n"); 
                percentage[i, j] = percentage_temp[j]; 
            }

            center_text_width = percentage_temp.Count(); 
        }
    } 
 
    private string[,] fontNumber = new string[,] { 
        { 
           " ???????  ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ",  
           "          ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ",  
           " ???????  ", 
        }, 
        { 
           "          ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           "          ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           "          ", 
        }, 
        { 
           " ???????  ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           " ???????  ", 
           "??        ", 
           "??        ", 
           "??        ", 
           "??        ", 
           "??        ",  
           " ???????  ", 
        }, 
        { 
           " ???????  ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           " ???????  ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           " ???????  ", 
        }, 
        { 
           "          ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ",  
           " ???????  ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           "          ", 
        }, 
        { 
           " ???????  ", 
           "??        ", 
           "??        ", 
           "??        ", 
           "??        ", 
           "??        ",  
           " ???????  ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           " ???????  ", 
        }, 
        { 
           " ???????  ", 
           "??        ", 
           "??        ", 
           "??        ", 
           "??        ", 
           "??        ",  
           " ???????  ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ",  
           " ???????  ", 
        }, 
        { 
           " ???????  ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           "          ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           "          ", 
        }, 
        { 
           " ???????  ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ",  
           " ???????  ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ",  
           " ???????  ", 
        }, 
        { 
           " ???????  ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ", 
           "??     ?? ",  
           " ???????  ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ", 
           "       ?? ",  
           " ???????  ", 
        } 
    };
}