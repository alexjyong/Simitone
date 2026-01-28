/*
 * Simitone Native Launcher for Linux/macOS
 * 
 * This is a minimal native executable that launches the actual Simitone
 * application from the lib/ subdirectory. This provides a clean user
 * experience with a single executable at the top level.
 * 
 * Compile with:
 *   Linux:  gcc -O2 -s launcher.c -o Simitone
 *   macOS:  clang -O2 launcher.c -o Simitone
 */

#define _GNU_SOURCE
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <libgen.h>
#include <limits.h>
#include <errno.h>

#ifdef __APPLE__
#include <mach-o/dyld.h>
#endif

/* Get the directory containing this executable */
static int get_exe_dir(char *buf, size_t size) {
#ifdef __APPLE__
    uint32_t bufsize = (uint32_t)size;
    if (_NSGetExecutablePath(buf, &bufsize) != 0) {
        return -1;
    }
    /* Resolve symlinks and get real path */
    char *real = realpath(buf, NULL);
    if (real) {
        strncpy(buf, real, size - 1);
        buf[size - 1] = '\0';
        free(real);
    }
#else
    /* Linux: read /proc/self/exe */
    ssize_t len = readlink("/proc/self/exe", buf, size - 1);
    if (len == -1) {
        return -1;
    }
    buf[len] = '\0';
#endif
    
    /* Get directory part */
    char *dir = dirname(buf);
    if (dir != buf) {
        memmove(buf, dir, strlen(dir) + 1);
    }
    return 0;
}

int main(int argc, char *argv[]) {
    char exe_dir[PATH_MAX];
    char lib_dir[PATH_MAX];
    char target_exe[PATH_MAX];
    
    /* Get the directory where this launcher is located */
    if (get_exe_dir(exe_dir, sizeof(exe_dir)) != 0) {
        fprintf(stderr, "Error: Could not determine launcher location.\n");
        return 1;
    }
    
    /* Build path to lib directory */
    snprintf(lib_dir, sizeof(lib_dir), "%s/lib", exe_dir);
    
    /* Build path to target executable */
    snprintf(target_exe, sizeof(target_exe), "%s/lib/Simitone", exe_dir);
    
    /* Check if target exists */
    if (access(target_exe, X_OK) != 0) {
        fprintf(stderr, "Error: Cannot find Simitone executable.\n");
        fprintf(stderr, "Expected location: %s\n", target_exe);
        fprintf(stderr, "\nPlease ensure the 'lib' folder exists and contains the game files.\n");
        return 1;
    }
    
    /* Change to lib directory so relative paths work correctly */
    if (chdir(lib_dir) != 0) {
        fprintf(stderr, "Error: Could not change to lib directory: %s\n", strerror(errno));
        return 1;
    }
    
    /* Replace this process with the actual Simitone executable */
    /* argv[0] will be replaced with the target path */
    argv[0] = target_exe;
    execv(target_exe, argv);
    
    /* If we get here, execv failed */
    fprintf(stderr, "Error: Failed to launch Simitone: %s\n", strerror(errno));
    return 1;
}
