/*
    written by David Lee, March 2023
    File systems allocate space in clusters, which are groups of sectors. for example:
    (using the dr app in a sister repo)

        D:\>dr
                  type     fs                volume         total          free    bps    spc
         c:      fixed   ntfs                c_boot    1,897,370m    1,174,303m    512      8
         d:      fixed   ntfs            d_ssd_24tb   22,892,603m   14,723,578m    512     16
         e:  removable  fat32               WALKMAN       12,554m       12,080m    512     64
         f:  removable  exfat               walkman      976,312m      418,402m    512  1,024
         g:  removable  exfat           EOS_DIGITAL      488,090m      485,428m    512    512
         s:      fixed   ntfs         s_ssd_4tb_pci    3,815,453m    3,633,342m    512      8
         y:      fixed   ntfs       y_wd_12tb_dcopy   11,444,093m    3,211,303m    512      8
         z:      fixed   ntfs         z_4tb_ssd_far    3,815,453m      224,957m    512      8

     bps = bytes per sector. spc = sectors per cluster.
     Disks allocate space with a minimum granularity of one cluster. For the examples above,
     one cluster is (512 bps * 8 spc) = 4k on C:, 32K on d:, and 512k on F:.
     When a file doesn't use the entire last cluster allocated, the extra space is wasted.
     That means a 1-byte file on F: consumes half a megabyte.
     For the music and album cover image files on that drive, this is the waste:

         D:\>cluwaste f:\

         examining: F:\
         total disk capacity:                  1,023,737,331,712
           cluster size:                                 524,288
           free:                                 438,726,295,552
           in use:                               585,011,036,160
         files examined:                                  22,603
           space in use:                         577,516,412,338
           wasted space in final clusters:         6,048,637,518
           percent wasted space:                            1.05%

     So it turns out that with mostly big .flac files, there really isn't that much waste.
*/

using System.Runtime.InteropServices;

class ClusterWastedSpace
{
    static void Usage( string context = null )
    {
        if ( null != context )
            Console.WriteLine( @"Context: {0}", context );

        Console.WriteLine( @"Usage: cluwaste [-s] [-a] <rootpath> <filespec>" );
        Console.WriteLine( @"  shows disk space wasted in unused last-cluster allocations" );
        Console.WriteLine( @"  arguments:  <rootpath> Optional path to start the enumeration. Default is current drive root \" );
        Console.WriteLine( @"              <filespec> Optional file filter, e.g. *.jpg. Default is *" );
        Console.WriteLine( @"              [-s]       Single-threaded. Slower; to see how much multiple cores help." );
        Console.WriteLine( @"  examples:   cluwaste f:\" );
        Console.WriteLine( @"              cluwaste f:\albums *.flac" );

        Environment.Exit( 1 );
    } //Usage

    static ulong filesExamined = 0;
    static ulong totalWasted = 0;
    static ulong totalUsed = 0;
    static ulong clusterSize = 0;

    static void Enumerate( DirectoryInfo diRoot, string spec, ParallelOptions po )
    {
        try
        {
            Parallel.ForEach( diRoot.EnumerateDirectories(), po, ( subDir ) =>
            {
                Enumerate( subDir, spec, po );
            });
        }
        catch ( Exception )
        {
            // Some folders are locked-down and can't be enumerated. Ignore them.
        }

        try
        {
            Parallel.ForEach( diRoot.EnumerateFiles( spec ), po, ( fileInfo ) =>
            {
                ulong waste = 0;
                ulong mod = (ulong) fileInfo.Length % clusterSize;
                if ( 0 != mod )
                    waste = clusterSize - ( (ulong) fileInfo.Length % clusterSize );

                Interlocked.Increment( ref filesExamined );
                Interlocked.Add( ref totalWasted, waste );
                Interlocked.Add( ref totalUsed, (ulong) fileInfo.Length );
            });
        }
        catch ( Exception )
        {
            // Some files are locked-down and can't be enumerated. Ignore them.
        }
    } //Enumerate

    [DllImport("kernel32.dll", SetLastError=true, CharSet=CharSet.Auto)]
    static extern bool GetDiskFreeSpace( string lpRootPathName,
                                         out ulong lpSectorsPerCluster,
                                         out ulong lpBytesPerSector,
                                         out ulong lpNumberOfFreeClusters,
                                         out ulong lpTotalNumberOfClusters );

    static void Main( string[] args )
    {
        if ( args.Count() > 4 )
            Usage( "argument count must be 1..4" );

        string root = null;
        string filespec = null;
        bool singleThreaded = false;

        for ( int i = 0; i < args.Length; i++ )
        {
            if ( ( '-' == args[i][0] ) || ( '/' == args[i][0] ) )
            {
                string argUpper = args[i].ToUpper();
                char c = argUpper[1];

                if ( 'S' == c )
                    singleThreaded = true;
                else
                    Usage( "argument isn't -s, so it's unrecognized" );
            }
            else
            {
                if ( null == root )
                    root = args[ i ];
                else if ( null == filespec )
                    filespec = args[ i ];
                else
                    Usage( "filespec and root both defined yet there is another argument: " + args[ i ] );
            }
        }

        if ( null == root )
            root = @"\";

        if ( null == filespec )
            filespec = @"*";

        root = Path.GetFullPath( root );
        Console.WriteLine( "examining: {0}", root );

        DirectoryInfo diRoot = new DirectoryInfo( root );
        ParallelOptions po = new ParallelOptions { MaxDegreeOfParallelism = singleThreaded ? 1 : -1 };

        ulong sectorsPerCluster, bytesPerSector, numberOfFreeClusters, totalNumberOfClusters;
        if ( GetDiskFreeSpace( root.Substring( 0, 2 ).ToLower(),
                               out sectorsPerCluster,
                               out bytesPerSector,
                               out numberOfFreeClusters,
                               out totalNumberOfClusters ) )
        {
            clusterSize = bytesPerSector * sectorsPerCluster;

            if ( 0 != clusterSize )
            {
                Enumerate( diRoot, filespec, po );

                Console.WriteLine( "total disk capacity:              {0,21:n0}", totalNumberOfClusters * clusterSize );
                Console.WriteLine( "  cluster size:                   {0,21:n0}", clusterSize );
                Console.WriteLine( "  free:                           {0,21:n0}", numberOfFreeClusters * clusterSize );
                Console.WriteLine( "  in use:                         {0,21:n0}", ( totalNumberOfClusters - numberOfFreeClusters ) * clusterSize );
                Console.WriteLine( "files examined:                   {0,21:n0}", filesExamined );
                Console.WriteLine( "  space in use:                   {0,21:n0}", totalUsed );
                Console.WriteLine( "  wasted space in final clusters: {0,21:n0}", totalWasted );
    
                if ( 0 != totalUsed )
                {
                    double dWaste = 100.0 * (double) totalWasted / (double) totalUsed;
                    Console.WriteLine( "  percent wasted space:           {0,21:N}%", dWaste );
                }
            }
            else
                Console.WriteLine( "disk geometry information is incorrect" );
        }
        else
            Console.WriteLine( "unable to get disk geometry information" );
    } //Main
} //ClusterWastedSpace

