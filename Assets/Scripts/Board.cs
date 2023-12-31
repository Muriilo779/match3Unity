using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Board : MonoBehaviour
{
    //Define the size of the board
    public int width = 6;
    public int height = 8;

    //Define some spacing for the board
    public float spacingX;
    public float spacingY;
    //Get a reference to our candies prefabs
    [SerializeField] public GameObject[] candiesPrefabs;
    //Get a reference to the collection of backgroundTile board + GO
    private BackgroundTile[,]  backgroundTiles;

    //Get a reference to the backgroundtiles obj
    [SerializeField] public GameObject backgroundObj;

    [SerializeField] public GameObject candiesBoardGO;
    //layoutArray
    [SerializeField] public ArrayLayout arrayLayout;
    //public static of board
    public static Board Instance;
    //List to hold the value of the candies that will be destroyed 
    public List<GameObject> candiesToDestroy = new();

    //The candy being selected 
    [SerializeField] private Candy selectedCandy;

    //Getting the gameObj of the parent of the candies
    [SerializeField] public GameObject candyParent;

    //Is it already moving?
    [SerializeField] private bool isProcessingMove;

    //The list have the candies were going to remove
    [SerializeField] private List<Candy> candiesToRemove = new();

    [SerializeField] private GameObject background;

    //Unity method that initialize the code when the obj with this script is called
    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        InitializeBoard();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.collider != null && hit.collider.gameObject.GetComponent<Candy>())
            {
                //If we're already moving a piece
                if (isProcessingMove) return;

                Candy candy = hit.collider.gameObject.GetComponent<Candy>();
               // Debug.Log("I have clicked a candy it is:" + candy.gameObject);

                SelectCandy(candy);
            }
        }
    }
    void InitializeBoard()
    {
        DestroyCandies();
        //Settign the size of this 2d array in this variable
        backgroundTiles = new BackgroundTile[width, height];

        //Variables that control where should the candies be placed
        spacingX = (float)(width - 1) / 2 - 2; //2.5
        spacingY = (float)(height - 1) / 2; //3.5

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++) 
            {
                //pos for the candy 
                Vector2 position = new Vector2(x - spacingX, y - spacingY);


                /*If I checked the buttons in the inspector of this obj, the tile with the
                same pos as the inspector button will not work and will not have a candy on it*/
                if (arrayLayout.rows[y].row[x])
                {
                    backgroundTiles[x, y] = new BackgroundTile(false, null);
                }
                else 
                {
                    //random number
                    int randomIndex = UnityEngine.Random.Range(0, candiesPrefabs.Length);

                    GameObject candy = Instantiate(candiesPrefabs[randomIndex], position, Quaternion.identity);
                    candy.GetComponent<Candy>().SetIndicies(x, y);
                    candy.transform.SetParent(candyParent.transform);
                    backgroundTiles[x, y] = new BackgroundTile(true, candy);
                    candiesToDestroy.Add(candy);
                }
                
            }
        }
        //Its not a match so dont remove and refill the board
        if(CheckBoard())
        {
            //Debug.Log("We have matches lets recreate the board");
            InitializeBoard();
        }
        else 
        {
            CreateBackground();
        }
    }

    private void DestroyCandies()
    {
        if(candiesToDestroy != null)
        {
            foreach(GameObject candy in candiesToDestroy)
            {
                Destroy(candy);
            }
            candiesToDestroy.Clear();
        }
    }
     
    private void CreateBackground()
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 position = new Vector3(x - spacingX, y - spacingY, 1);

                if (backgroundTiles[x, y].isUsable == true)
                {
                    Instantiate(background, position, Quaternion.identity);
                }

            }
        }
    }
    //If we have a match 
    public bool CheckBoard()
    {
        //If the game ended do not do matches anymore
        if (GameManager.Instance.isGameEnded)
            return false;

        Debug.Log("Checking Board");
        //If we have at least 1 check at the board and we have a match 
        bool hasMatched = false;

        //cleaning the list created by the class that have the candies matched
        candiesToRemove.Clear();

        foreach(BackgroundTile backgroundTile in backgroundTiles)
        {
            if(backgroundTile.candy != null)
            {
                backgroundTile.candy.GetComponent<Candy>().isMatched = false;
            }
        }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                //Checking if the tile is usable
                if (backgroundTiles[x, y].isUsable == true && backgroundTiles[x, y].candy != null)
                {
                    //Then proceed to get candy class in tile
                    Candy candy = backgroundTiles[x, y].candy.GetComponent<Candy>(); 

                    //Ensure its not matched
                    if(!candy.isMatched)
                    {
                        //Run some matching logic

                        MatchResult matchedCandies= IsConnected(candy);

                        //If we had a match 
                        if(matchedCandies.connectedCandies.Count >= 3)
                        {
                            //Take the class inside matchesCandies var and put in the method supermatch to see if has a match
                            MatchResult superMatchedCandies = SuperMatch(matchedCandies);
                            //put them in a list that we've created early in this function 
                            candiesToRemove.AddRange(superMatchedCandies.connectedCandies);

                            //And for each candy set their variable isMatched to true 
                            foreach (Candy cand in superMatchedCandies.connectedCandies)
                            {
                                cand.isMatched = true;
                            }
                            hasMatched = true;
                        }
                    }
                }
            }
        }

        return hasMatched;
    }

    public IEnumerator ProcessTurnOnMatchedBoard(bool _subtractMoves)
    {
        foreach (Candy candyToRemove in candiesToRemove)
        {
            candyToRemove.isMatched = false;
        }

        RemoveAndRefill(candiesToRemove);

        GameManager.Instance.ProcessTurn(candiesToRemove.Count, _subtractMoves);

        yield return new WaitForSeconds(0.6f);

        if (CheckBoard())
        {
            StartCoroutine(ProcessTurnOnMatchedBoard(false));
        }
    }

    private void RemoveAndRefill(List<Candy> _candiesToRemove)
    {
        //Removing the candy and clearing the board at that location
        foreach(Candy candy in _candiesToRemove)
        {
            //Getting it's x and y indicies and storing them
            int _xIndex = candy.xIndex;
            int _yIndex = candy.yIndex;

            //Destroy the candy
            Destroy(candy.gameObject);

            backgroundTiles[_xIndex, _yIndex] = new BackgroundTile(true, null);

            
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                //In the empty space that is usable and have no candies refill it
                if(backgroundTiles[x, y].candy == null && backgroundTiles[x, y].isUsable == true)
                {

                    //Debug.Log("The location X: " + x + " Y: " + y + " is empty, attempting to refill it");
                    RefillCandy(x, y);

                }
            }
        }
    }


    private MatchResult SuperMatch(MatchResult _matchedResults)
    {
        //if the class used as a argument here have a horizontal or long horizontal match
        if (_matchedResults.direction == MatchDirection.Horizontal || _matchedResults.direction == MatchDirection.LongHorizontal)
        {
            //loop through the candies in my match
            foreach (Candy candy in _matchedResults.connectedCandies)
            {
                //Create a new list of candies 'extra matches'
                List<Candy> extraConnectedCandies = new();
                //CheckDirection up
                CheckDirection(candy, new Vector2Int(0, 1), extraConnectedCandies);
                //CheckDirection down
                CheckDirection(candy, new Vector2Int(0, -1), extraConnectedCandies);

                //Do we have 2 or more extra matches.
                if (extraConnectedCandies.Count >= 2)
                {
                  //  Debug.Log("I have a super Horizontal Match");
                    extraConnectedCandies.AddRange(_matchedResults.connectedCandies);

                    //we've made a super match - retunr a new matchresult of type super
                    return new MatchResult
                    {
                        connectedCandies = extraConnectedCandies,
                        direction = MatchDirection.Super
                    };
                }
            }
            return new MatchResult
            {
                connectedCandies = _matchedResults.connectedCandies,
                direction = _matchedResults.direction
            };
        }

        //if the class used as a argument here have a vertical or long vertical match
        else if (_matchedResults.direction == MatchDirection.Vertical || _matchedResults.direction == MatchDirection.LongVertical)
        {
            //loop through the candies in my match
            foreach (Candy candy in _matchedResults.connectedCandies)
            {
                //Create a new list of candies 'extra matches'
                List<Candy> extraConnectedCandies = new();
                //CheckDirection left
                CheckDirection(candy, new Vector2Int(-1, 0), extraConnectedCandies);
                //CheckDirection right
                CheckDirection(candy, new Vector2Int(1, 0), extraConnectedCandies);

                //Do we have 2 or more extra matches.
                if (extraConnectedCandies.Count >= 2)
                {
                   // Debug.Log("I have a super Vertical Match");
                    extraConnectedCandies.AddRange(_matchedResults.connectedCandies);

                    //we've made a super match - retunr a new matchresult of type super
                    return new MatchResult
                    {
                        connectedCandies = extraConnectedCandies,
                        direction = MatchDirection.Super
                    };
                }
            }
            return new MatchResult
            {
                connectedCandies = _matchedResults.connectedCandies,
                direction = _matchedResults.direction
            };
        }
        return null;
    }

    //Is connected with the candies of same type
    MatchResult IsConnected(Candy candy) 
    {
        //Creating a list of candies connected 
        List<Candy> connectedCandies = new();

        //The color setted in the enum inside the candy script
        CandyColor candyColor = candy.candyColor;

        //Adding the candy 
        connectedCandies.Add(candy);

        //check right
        CheckDirection(candy, new Vector2Int(1, 0), connectedCandies);
        //check left
        CheckDirection(candy, new Vector2Int(-1, 0), connectedCandies);
        //have we made a 3 match? (Horizontal Match)
        if(connectedCandies.Count == 3)
        {
           // Debug.Log("I have a normal horizontal match, the color of my match is: " + connectedCandies[0].candyColor);
            //Return the class that i've create below, and this class is going to collect the list of candies connected and the direction of the match
            return new MatchResult
            {
                connectedCandies = connectedCandies,
                direction = MatchDirection.Horizontal
            };
        }
        //Checking for more than 3 (Long horizontal match)
        else if(connectedCandies.Count > 3)
        {
            //Debug.Log("I have a long horizontal match, the color of my match is: " + connectedCandies[0].candyColor);
            //Return the class that i've create below, and this class is going to collect the list of candies connected and the direction of the match
            return new MatchResult
            {
                connectedCandies = connectedCandies,
                direction = MatchDirection.LongHorizontal
            };
        };
        //clear out the connectedCandies
        connectedCandies.Clear();
        //read our initial candy
        connectedCandies.Add(candy);

        //check up
        CheckDirection(candy, new Vector2Int(0, 1), connectedCandies);
        //check down
        CheckDirection(candy, new Vector2Int(0, -1), connectedCandies);
        //have we made a 3 match? (Vertical Match)
        if (connectedCandies.Count == 3)
        {
            //Debug.Log("I have a normal vertical match, the color of my match is:" + connectedCandies[0].candyColor);
            //Return the class that i've create below, and this class is going to collect the list of candies connected and the direction of the match
            return new MatchResult
            {
                connectedCandies = connectedCandies,
                direction = MatchDirection.Vertical
            };
        }
        //Checking for more than 3 (Long vertical match)
        else if (connectedCandies.Count > 3)
        {
           // Debug.Log("I have a long vertical match, the color of my match is: " + connectedCandies[0].candyColor);
            //Return the class that i've create below, and this class is going to collect the list of candies connected and the direction of the match
            return new MatchResult
            {
                connectedCandies = connectedCandies,
                direction = MatchDirection.LongVertical
            };
        }
        //if we had no matches
        else 
        {
            //Return the class that i've create below, and this class is going to collect the list of candies connected (0) and the direction of the match (none)
            return new MatchResult
            {
                connectedCandies = connectedCandies,
                direction = MatchDirection.None
            };
        }
    }
    //Check Direction of this connection
    void CheckDirection(Candy candy, Vector2Int direction, List<Candy> connectedCandies)
    {
        //The variable candycolor has the value of the color of the candy passed at this method
        CandyColor candyColor = candy.candyColor;

        //the x position of this candy + the position of the next tile/candy
        //If I wanna check the right candy I do myPos + 1, finding the position of that candy
        int x = candy.xIndex + direction.x;
        int y = candy.yIndex + direction.y;

        //Check that we're within the boundaries of the board
        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            if (backgroundTiles[x, y].isUsable == true && backgroundTiles[x, y].candy != null)
            {
                //Debug.Log("Checking the candie in the x and y pos: " + x + " " + y);
                //Getting the candy next to us
                Candy otherCandy = backgroundTiles[x, y].candy.GetComponent<Candy>();

                //Does our candyColor Match? it must also not be matched
                if (!otherCandy.isMatched && otherCandy.candyColor == candyColor)
                {
                    //putting the other candy in the list of connedcted candies
                    connectedCandies.Add(otherCandy);

                    //Adding value to our x and y to continue the loop 
                    x += direction.x;
                    y += direction.y;
                }
                else break;
            }
            else break;
        }
    }
    #region Swapping candies
    //Select a candy
    public void SelectCandy(Candy _candy) 
    {
        //If we don�t have a candy currently selected, then set the candy I just clicked to my selectedCandy
        if(selectedCandy == null)
        {
            Debug.Log(_candy);
            selectedCandy = _candy;
        }
        //If we select the same candy twice selectedCandy = null 
        else if (selectedCandy == _candy) 
        {
            selectedCandy = null;
        }
        //If selectedCandy is not null and its not in the current position, attempt a swap 
        //selectedCandy back to null 
        else if(selectedCandy != _candy) 
        {
            SwapCandy(selectedCandy, _candy);
            selectedCandy = null;
        }
    }

    //Swap a candy  - logic
    private void SwapCandy(Candy _currentCandy, Candy _targetCandy)
    {   
        //if its not adjacent dont do anything
        if(!IsAdjacent(_currentCandy, _targetCandy)) { return; }

        //DoSwap
        DoSwap(_currentCandy, _targetCandy);

        isProcessingMove = true;
        //StartCourotine ProcessMatches, have I got a match? If yes do one thing if no do another thing 
        StartCoroutine(ProcessMatches(_currentCandy, _targetCandy));

    }

    //Do swap
    private void DoSwap(Candy _currentCandy, Candy _targetCandy)
    {
        //storing the location of the first candy 
        GameObject temp = backgroundTiles[_currentCandy.xIndex, _currentCandy.yIndex].candy;

        //Changing the index of the current candy to the target one 
        backgroundTiles[_currentCandy.xIndex, _currentCandy.yIndex].candy = backgroundTiles[_targetCandy.xIndex, _targetCandy.yIndex].candy;

        backgroundTiles[_targetCandy.xIndex, _targetCandy.yIndex].candy = temp;

        //update indicies
        //Save the current x and y in the temp
        int tempXIndex = _currentCandy.xIndex;
        int tempYIndex = _currentCandy.yIndex;
        //Now change the currentcandy x and y to the target x and y 
        _currentCandy.xIndex = _targetCandy.xIndex;
        _currentCandy.yIndex = _targetCandy.yIndex;
        //And now change the x and y of the target to the current. It means that I stored the current candy X and Y in temp variable, and then swap both current and target X's and Y's
        _targetCandy.xIndex = tempXIndex;
        _targetCandy.yIndex = tempYIndex;

        //Move the candy 
        _currentCandy.MoveToTarget(backgroundTiles[_targetCandy.xIndex, _targetCandy.yIndex].candy.transform.position);
        _targetCandy.MoveToTarget(backgroundTiles[_currentCandy.xIndex, _currentCandy.yIndex].candy.transform.position);
    }

    private IEnumerator ProcessMatches(Candy _currentCandy, Candy _targetCandy)
    {
        //Wait for some seconds
        yield return new WaitForSeconds(0.3f);

        if(CheckBoard())
        {
            //Start coroutine to process the matches in our turn
            StartCoroutine(ProcessTurnOnMatchedBoard(true));
        }
        else
        {
            DoSwap(_currentCandy, _targetCandy);
        }

        isProcessingMove = false;
    }
    //isAdjecent
    private bool IsAdjacent(Candy _currentCandy, Candy _targetCandy)
    {
        return Mathf.Abs(_currentCandy.xIndex - _targetCandy.xIndex) + Mathf.Abs(_currentCandy.yIndex - _targetCandy.yIndex) == 1;
    }
    //ProcessMatches
    #endregion

    #region Cascading Candies
    private void RefillCandy(int x, int y)
    {
        //y offset 
        int yOffset = 1;

        //While the cell above our current cell have no candy and is usable and we're below the height of the board OR the cell above us is not usable and we're below the height of the boards
        while ((y + yOffset < height && backgroundTiles[x, y + yOffset].candy == null && backgroundTiles[x, y + yOffset].isUsable == true) || (y + yOffset < height && backgroundTiles[x, y + yOffset].isUsable == false))
        {
            //Increment y offset
            Debug.Log("The candy abover me is null and Im not at the top of the board yet, so Ill add to my yOffset and try again " + yOffset + "and my x and y position is: " + x + " " + y);
            yOffset++;
        }


        if (y + yOffset < height && backgroundTiles[x, y + yOffset].candy != null)
        {
            //we've found a candy
            Candy candyAbove = backgroundTiles[x, y + yOffset].candy.GetComponent<Candy>();

            //Getting the location
            Vector3 targetPos = new Vector3(x - spacingX, y - spacingY, candyAbove.transform.position.z);

            //Move it to the correct location
            candyAbove.MoveToTarget(targetPos);

            //Update indicies
            candyAbove.SetIndicies(x, y);

            //Update our backgroundTiles
            backgroundTiles[x, y] = backgroundTiles[x, y + yOffset];

            //Set the location the candy came from to null
            backgroundTiles[x, y + yOffset] = new BackgroundTile(true, null);
        }

        //If we've hit the top of the board without finding a candy 
        if(y + yOffset == height)
        {
            Debug.Log("I have reached the top of the board without finding a candy at the x location of:" + x + "and y: " + y);
            SpawnCandyAtTop(x);
        }
    }

    private void SpawnCandyAtTop(int x)
    {
        int index = FindIndexOfLowestNull(x);
        //How mucn we need to go down
        int locationToMoveTo = 8 - index;

        Debug.Log("About to spawn a candy, ideally i'd like to put it in the index of: " + index + " and the x pos is:" + x );

        //Get a random candy
        int randomIndex = UnityEngine.Random.Range(0, candiesPrefabs.Length); 
        GameObject newCandy = Instantiate(candiesPrefabs[randomIndex], new Vector2(x - spacingX, height - spacingY), Quaternion.identity);

        //Setting the parent
        newCandy.transform.SetParent(candyParent.transform);

        //Set indicies
        newCandy.GetComponent<Candy>().SetIndicies(x, index);

        //Set it on the board
        backgroundTiles[x, index] = new BackgroundTile(true, newCandy);

        //Move it to that position
        Vector3 newTargetPos = new Vector3(newCandy.transform.position.x, newCandy.transform.position.y - locationToMoveTo, newCandy.transform.position.z);

        newCandy.GetComponent<Candy>().MoveToTarget(newTargetPos);
    }

    private int FindIndexOfLowestNull(int x)
    {
        int lowestNull = 99;
        for(int y = 7; y >= 0; y--)
        {
            if (backgroundTiles[x, y].candy == null && backgroundTiles[x, y].isUsable == true)
            {
                lowestNull = y;
            }
        }
        return lowestNull;
    }
    #endregion
}



//To get easy to read the match result
public class MatchResult
{
    public List<Candy> connectedCandies;
    public MatchDirection direction;
}

public enum MatchDirection
{
    Vertical,
    Horizontal,
    LongVertical,
    LongHorizontal,
    Super,
    None
}
               