using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

internal class UsersSearchUserViewsHandler : IDisposable
{
    private readonly Dictionary<string, UserElementView> userElementViews = new Dictionary<string, UserElementView>();
    private readonly Queue<UserElementView> viewPool = new Queue<UserElementView>();

    private readonly UserElementView userElementViewBase;
    private readonly Transform elementsParent;
    
    private string[] usersInRolList;
    
    public int userElementViewCount => userElementViews.Count;

    public UsersSearchUserViewsHandler(UserElementView userElementViewBase, Transform elementsParent)
    {
        this.userElementViewBase = userElementViewBase;
        this.elementsParent = elementsParent;
        PoolUserView(userElementViewBase);
    }
    public void Dispose()
    {
        foreach (UserElementView userView in userElementViews.Values)
        {
            userView.Dispose();
        }
        userElementViews.Clear();

        while (viewPool.Count > 0)
        {
            viewPool.Dequeue().Dispose();
        }
    }
    
    public void SetUserViewsList(List<UserProfile> profiles)
    {
        UserProfile profile;
        for (int i = 0; i < profiles.Count; i++)
        {
            profile = profiles[i];
            if (profile == null) 
                continue;
            
            bool isBlocked = UserProfile.GetOwnUserProfile().blocked.Contains(profile.userId);
            bool isAddedRol = usersInRolList?.Contains(profile.userId) ?? false;

            if (!userElementViews.TryGetValue(profile.userId, out UserElementView userElementView))
            {
                userElementView = GetUserView();
                userElementView.SetUserProfile(profile);
                userElementView.SetAlwaysHighlighted(true);
                userElementViews.Add(profile.userId, userElementView);
            }
            
            userElementView.SetBlocked(isBlocked);
            userElementView.SetIsAdded(isAddedRol);
        }
    }

    public void RemoveUserView(string userId)
    {
        if (userElementViews.TryGetValue(userId, out UserElementView userElementView))
        {
            PoolUserView(userElementView);
        }
    }

    public void SetVisibleList(List<UserElementView> viewsList)
    {
        if (viewsList == null)
        {
            return;
        }
        
        foreach (UserElementView userElementView in userElementViews.Values)
        {
            userElementView.SetActive(false);
        }
        for (int i = 0; i < viewsList.Count; i++)
        {
            viewsList[i].SetActive(true);
            viewsList[i].SetOrder(i);
        }
    }

    public void SetUsersInRolList(string[] usersId)
    {
        usersInRolList = usersId;
        foreach (KeyValuePair<string, UserElementView> keyValuePair in userElementViews)
        {
            keyValuePair.Value.SetIsAdded(usersId.Contains(keyValuePair.Key));
        }
    }

    public List<UserElementView> GetUserElementViews()
    {
        return userElementViews.Values.ToList();
    }

    private void PoolUserView(UserElementView userView)
    {
        userView.SetActive(false);
        viewPool.Enqueue(userView);
    }

    private UserElementView GetUserView()
    {
        UserElementView userView;

        if (viewPool.Count > 0)
        {
            userView = viewPool.Dequeue();
        }
        else
        {
            userView = Object.Instantiate(userElementViewBase, elementsParent);
        }

        return userView;
    }
}