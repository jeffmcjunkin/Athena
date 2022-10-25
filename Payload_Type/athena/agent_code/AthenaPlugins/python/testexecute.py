import itertools
import math
import json 
import os

def Execute():
    print("Lib imported")
    print(1 + 1)
    a = 4
    b = 2
    c = a + b
    print(c)
    print("Attempting To do JSON")
    print(json.dumps({"4": 5, "6": 7}, sort_keys=True, indent=4))
    print("Attempting To Run KiranLib stuff for external lib testing")
    print("Running System Command for testing.")
    os.system('whoami')
    return "Le fin"

print("Finished Left function")