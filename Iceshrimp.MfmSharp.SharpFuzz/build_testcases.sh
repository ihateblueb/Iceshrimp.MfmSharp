#!/bin/bash
i=0
while IFS="" read -r p || [ -n "$p" ]
do
  ((i++))
  echo -e "$p" > "TestCases/$i.txt"
done < tests.txt
