# cluwaste
Windows command-line tool to see how much space is wasted in the final cluster of files on a drive.

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

I build this tool using .net 6 and the included m.bat and .csproj files.
